using System.Data;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Выдаёт награды за квесты и мероприятия.
/// Факт выдачи фиксируется в <c>reward_claimed</c> атомарно, поэтому повторный вызов
/// не приводит к повторному начислению.
/// </summary>
public sealed class ActivityRewardService(
    AppDbContext dbContext,
    ActivityRewardGranter rewardGranter) : IActivityRewardService
{
    /// <inheritdoc />
    public async Task<ServiceResult<ActivityRewardResult>> GrantQuestRewardAsync(
        long questParticipationId,
        CancellationToken cancellationToken = default)
    {
        if (questParticipationId <= 0)
        {
            return ServiceResult<ActivityRewardResult>.Fail(400, "A valid quest participation ID is required.");
        }

        return await ExecuteInTransactionAsync(
            () => GrantQuestRewardCoreAsync(questParticipationId, cancellationToken),
            cancellationToken);
    }

    private async Task<ServiceResult<ActivityRewardResult>> GrantQuestRewardCoreAsync(
        long questParticipationId,
        CancellationToken cancellationToken)
    {
        var participation = await dbContext.QuestParticipations
            .AsNoTracking()
            .Include(participation => participation.Quest)
            .SingleOrDefaultAsync(participation => participation.Id == questParticipationId, cancellationToken);

        if (participation is null)
        {
            return ServiceResult<ActivityRewardResult>.Fail(404, "Quest participation was not found.");
        }

        if (participation.RewardClaimed)
        {
            return AlreadyGranted(participation.UserId);
        }

        if (!string.Equals(participation.Status, QuestParticipationStatuses.Completed, StringComparison.Ordinal))
        {
            return ServiceResult<ActivityRewardResult>.Fail(409, "The quest is not completed.");
        }

        if (participation.Quest.RewardAmount < 0)
        {
            return ServiceResult<ActivityRewardResult>.Fail(409, "The quest has an invalid reward amount.");
        }

        var claimed = await dbContext.QuestParticipations
            .Where(participation => participation.Id == questParticipationId
                                    && !participation.RewardClaimed
                                    && participation.Status == QuestParticipationStatuses.Completed)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(participation => participation.RewardClaimed, true),
                cancellationToken);

        if (claimed == 0)
        {
            return AlreadyGranted(participation.UserId);
        }

        // Нулевая награда обрабатывается так же, как платная: флаг фиксируется атомарно,
        // но транзакция в ledger не создаётся. Тот же порядок использует завершение квеста,
        // поэтому оба пути одинаково понимают «награда обработана».
        if (participation.Quest.RewardAmount == 0)
        {
            return ServiceResult<ActivityRewardResult>.Ok(
                new ActivityRewardResult(participation.UserId, 0, null, false));
        }

        var credit = await rewardGranter.GrantAsync(
            participation.UserId,
            participation.Quest.RewardAmount,
            $"Награда за квест \"{participation.Quest.Title}\"",
            ActivityRewardEntityTypes.Quest,
            participation.Id,
            cancellationToken);

        if (!credit.IsSuccess)
        {
            return ServiceResult<ActivityRewardResult>.Fail(credit.StatusCode, credit.Error!);
        }

        return ServiceResult<ActivityRewardResult>.Ok(new ActivityRewardResult(
            participation.UserId,
            participation.Quest.RewardAmount,
            credit.Value!.Amount,
            false));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ActivityRewardResult>> GrantEventRewardAsync(
        long eventParticipantId,
        CancellationToken cancellationToken = default)
    {
        if (eventParticipantId <= 0)
        {
            return ServiceResult<ActivityRewardResult>.Fail(400, "A valid event participant ID is required.");
        }

        return await ExecuteInTransactionAsync(
            () => GrantEventRewardCoreAsync(eventParticipantId, cancellationToken),
            cancellationToken);
    }

    private async Task<ServiceResult<ActivityRewardResult>> GrantEventRewardCoreAsync(
        long eventParticipantId,
        CancellationToken cancellationToken)
    {
        var participant = await dbContext.EventParticipants
            .AsNoTracking()
            .Include(participant => participant.Event)
            .SingleOrDefaultAsync(participant => participant.Id == eventParticipantId, cancellationToken);

        if (participant is null)
        {
            return ServiceResult<ActivityRewardResult>.Fail(404, "Event participant was not found.");
        }

        if (participant.RewardClaimed)
        {
            return AlreadyGranted(participant.UserId);
        }

        if (!string.Equals(participant.Status, EventParticipantStatuses.Attended, StringComparison.Ordinal))
        {
            return ServiceResult<ActivityRewardResult>.Fail(409, "Event attendance is not confirmed.");
        }

        if (participant.Event.RewardAmount <= 0)
        {
            return ServiceResult<ActivityRewardResult>.Fail(409, "The event has no reward configured.");
        }

        var claimed = await dbContext.EventParticipants
            .Where(participant => participant.Id == eventParticipantId
                                  && !participant.RewardClaimed
                                  && participant.Status == EventParticipantStatuses.Attended)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(participant => participant.RewardClaimed, true),
                cancellationToken);

        if (claimed == 0)
        {
            return AlreadyGranted(participant.UserId);
        }

        var credit = await rewardGranter.GrantAsync(
            participant.UserId,
            participant.Event.RewardAmount,
            $"Награда за мероприятие \"{participant.Event.Title}\"",
            ActivityRewardEntityTypes.Event,
            participant.Id,
            cancellationToken);

        if (!credit.IsSuccess)
        {
            return ServiceResult<ActivityRewardResult>.Fail(credit.StatusCode, credit.Error!);
        }

        return ServiceResult<ActivityRewardResult>.Ok(new ActivityRewardResult(
            participant.UserId,
            participant.Event.RewardAmount,
            credit.Value!.Amount,
            false));
    }

    /// <summary>
    /// Выполняет операцию в транзакции уровня <see cref="IsolationLevel.Serializable"/>.
    /// Если вызывающий код уже открыл транзакцию, операция выполняется в её рамках.
    /// </summary>
    private async Task<ServiceResult<T>> ExecuteInTransactionAsync<T>(
        Func<Task<ServiceResult<T>>> operation,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation();
        }

        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var result = await operation();

        if (result.IsSuccess)
        {
            await databaseTransaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private static ServiceResult<ActivityRewardResult> AlreadyGranted(Guid userId) =>
        ServiceResult<ActivityRewardResult>.Ok(new ActivityRewardResult(userId, 0, null, true));
}
