using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexApp.Infrastructure.Services.Quests;

/// <summary>
/// Результат синхронизации автоматических шагов.
/// </summary>
/// <param name="CreatedCompletions">Сколько завершений создано этим вызовом.</param>
/// <param name="MisconfiguredSteps">Шаги с некорректной конфигурацией (fail-closed).</param>
public sealed record QuestProgressSyncResult(
    int CreatedCompletions,
    IReadOnlyList<string> MisconfiguredSteps)
{
    /// <summary>Пустой результат.</summary>
    public static QuestProgressSyncResult Empty { get; } = new(0, []);
}

/// <summary>
/// Сверяет автоматические шаги с фактическими записями БД и фиксирует выполнение.
/// Работает по принципу pull: результат не зависит от того, как именно было записано
/// исходное действие, и не опирается на уведомления или перехватчики EF.
/// </summary>
public interface IQuestProgressService
{
    /// <summary>
    /// Синхронизирует автоматические шаги активных участий пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="questIds">Квесты, для которых нужна сверка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<QuestProgressSyncResult> SyncAsync(
        Guid userId,
        IReadOnlyCollection<long> questIds,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class QuestProgressService(
    AppDbContext dbContext,
    IQuestStepVerifier verifier,
    ILogger<QuestProgressService> logger) : IQuestProgressService
{
    /// <inheritdoc />
    public async Task<QuestProgressSyncResult> SyncAsync(
        Guid userId,
        IReadOnlyCollection<long> questIds,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || questIds.Count == 0)
        {
            return QuestProgressSyncResult.Empty;
        }

        var requestedQuestIds = questIds
            .Where(questId => questId > 0)
            .Distinct()
            .ToArray();

        if (requestedQuestIds.Length == 0)
        {
            return QuestProgressSyncResult.Empty;
        }

        var now = DateTime.UtcNow;

        // Только активные участия доступных квестов: после окончания квеста новые
        // завершения не создаются, а уже сохранённые остаются частью истории.
        var participations = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.UserId == userId
                && requestedQuestIds.Contains(participation.QuestId)
                && participation.Status == QuestParticipationStatuses.InProgress
                && participation.Quest.IsActive
                && (participation.Quest.StartAt == null || participation.Quest.StartAt <= now)
                && (participation.Quest.EndAt == null || participation.Quest.EndAt >= now))
            .Select(participation => new
            {
                participation.Id,
                participation.QuestId,
                participation.StartedAt
            })
            .ToListAsync(cancellationToken);

        if (participations.Count == 0)
        {
            return QuestProgressSyncResult.Empty;
        }

        var steps = await dbContext.QuestSteps
            .AsNoTracking()
            .Where(step =>
                requestedQuestIds.Contains(step.QuestId)
                && step.CompletionType == QuestStepCompletionType.Automatic)
            .Select(step => new
            {
                step.Id,
                step.QuestId,
                step.Title,
                step.CompletionData
            })
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            return QuestProgressSyncResult.Empty;
        }

        var participationIds = participations.Select(participation => participation.Id).ToList();

        var existingCompletions = await dbContext.QuestStepCompletions
            .AsNoTracking()
            .Where(completion => participationIds.Contains(completion.ParticipationId))
            .Select(completion => new { completion.ParticipationId, completion.StepId })
            .ToListAsync(cancellationToken);

        var existing = existingCompletions
            .Select(completion => (completion.ParticipationId, completion.StepId))
            .ToHashSet();

        var participationByQuest = participations
            .ToDictionary(participation => participation.QuestId);

        var pending = new List<PendingStep>();
        var misconfigured = new List<string>();
        var reportedSteps = new HashSet<long>();

        foreach (var step in steps)
        {
            if (!participationByQuest.TryGetValue(step.QuestId, out var participation))
            {
                continue;
            }

            if (existing.Contains((participation.Id, step.Id)))
            {
                continue;
            }

            if (!QuestStepActionConfiguration.TryParse(
                    step.CompletionData,
                    out var configuration,
                    out var error)
                || configuration is null)
            {
                misconfigured.Add($"\"{step.Title}\": {error}");

                if (reportedSteps.Add(step.Id))
                {
                    logger.LogWarning(
                        "Quest step {StepId} has an invalid automatic configuration and was not evaluated: {Reason}",
                        step.Id,
                        error);
                }

                continue;
            }

            pending.Add(new PendingStep(
                participation.Id,
                participation.QuestId,
                step.Id,
                participation.StartedAt,
                configuration));
        }

        if (pending.Count == 0)
        {
            return new QuestProgressSyncResult(0, misconfigured);
        }

        // Один запрос на тип действия вместо запроса на каждый шаг.
        var verifications = await verifier.VerifyAsync(
            userId,
            pending
                .Select(item => new QuestStepVerificationRequest(
                    item.ParticipationId,
                    item.StepId,
                    item.QuestId,
                    item.StartedAt,
                    item.Configuration))
                .ToList(),
            cancellationToken);

        var created = 0;

        for (var index = 0; index < pending.Count; index++)
        {
            if (!verifications[index].IsSatisfied)
            {
                continue;
            }

            var item = pending[index];

            var completion = new QuestStepCompletion
            {
                ParticipationId = item.ParticipationId,
                QuestId = item.QuestId,
                StepId = item.StepId,
                CompletedAt = DateTime.UtcNow,
                Proof = verifications[index].EvidenceJson
            };

            dbContext.QuestStepCompletions.Add(completion);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                created++;
            }
            catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
            {
                // Конкурентная сверка уже создала это завершение. Уникальный индекс
                // (participation_id, step_id) не даёт создать дубликат.
                dbContext.Entry(completion).State = EntityState.Detached;
            }
        }

        if (created > 0)
        {
            logger.LogInformation(
                "Quest progress sync created {Count} automatic completion(s) for user {UserId}",
                created,
                userId);
        }

        return new QuestProgressSyncResult(created, misconfigured);
    }

    private sealed record PendingStep(
        long ParticipationId,
        long QuestId,
        long StepId,
        DateTime StartedAt,
        QuestStepActionConfiguration Configuration);
}
