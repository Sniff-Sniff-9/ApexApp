using System.Data;
using System.Text.Json;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Пользовательские сценарии квестов: просмотр доступных квестов, участие,
/// выполнение шагов и завершение квеста с начислением награды, а также
/// администрирование квестов, шагов и проверка доказательств.
/// </summary>
public sealed class QuestService(
    AppDbContext dbContext,
    IActivityRewardService activityRewardService) : IQuestService
{
    private const int TitleMaxLength = 200;

    /// <inheritdoc />
    public async Task<PagedResult<QuestSummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var now = DateTime.UtcNow;

        var query = dbContext.Quests
            .AsNoTracking()
            .Where(quest => quest.IsActive
                            && (quest.StartAt == null || quest.StartAt <= now)
                            && (quest.EndAt == null || quest.EndAt >= now));

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(quest => quest.CreatedAt)
            .ThenByDescending(quest => quest.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(quest => new
            {
                Quest = quest,
                StepsCount = quest.QuestSteps.Count,
                RequiredStepsCount = quest.QuestSteps.Count(step => step.IsRequired),
                ParticipationStatus = quest.QuestParticipations
                    .Where(participation => participation.UserId == userId)
                    .Select(participation => participation.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new QuestSummary(
                row.Quest,
                row.StepsCount,
                row.RequiredStepsCount,
                row.ParticipationStatus))
            .ToList();

        return new PagedResult<QuestSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestDetailsResult>> GetDetailsAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<QuestDetailsResult>.Fail(400, "A valid quest ID is required.");
        }

        var quest = await dbContext.Quests
            .AsNoTracking()
            .Include(quest => quest.QuestSteps)
            .FirstOrDefaultAsync(quest => quest.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<QuestDetailsResult>.Fail(404, "Quest was not found.");
        }

        var participation = await dbContext.QuestParticipations
            .AsNoTracking()
            .Include(participation => participation.QuestStepCompletions)
            .ThenInclude(completion => completion.QuestProofs)
            .FirstOrDefaultAsync(
                participation => participation.QuestId == questId && participation.UserId == userId,
                cancellationToken);

        if (participation is null && !IsAvailable(quest, DateTime.UtcNow))
        {
            return ServiceResult<QuestDetailsResult>.Fail(
                409,
                "Quest is not active or outside its date range.");
        }

        var steps = quest.QuestSteps
            .OrderBy(step => step.SortOrder)
            .ThenBy(step => step.Id)
            .Select(step =>
            {
                var completion = participation?.QuestStepCompletions
                    .Where(completion => completion.StepId == step.Id)
                    .OrderByDescending(completion => completion.CompletedAt)
                    .ThenByDescending(completion => completion.Id)
                    .FirstOrDefault();

                return new QuestStepState(step, completion, completion is null ? null : LatestProof(completion));
            })
            .ToList();

        return ServiceResult<QuestDetailsResult>.Ok(
            new QuestDetailsResult(quest, participation, steps));
    }

    /// <inheritdoc />
    public async Task<PagedResult<MyQuestSummary>> GetMyAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation => participation.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var participations = await query
            .OrderByDescending(participation => participation.StartedAt)
            .ThenByDescending(participation => participation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(participation => participation.Quest)
            .ThenInclude(quest => quest.QuestSteps)
            .Include(participation => participation.QuestStepCompletions)
            .ThenInclude(completion => completion.QuestProofs)
            .ToListAsync(cancellationToken);

        var items = participations
            .Select(participation => new MyQuestSummary(
                participation,
                participation.Quest.QuestSteps.Count,
                participation.Quest.QuestSteps.Count(step =>
                    IsStepSatisfied(step, participation.QuestStepCompletions))))
            .ToList();

        return new PagedResult<MyQuestSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestJoinResult>> JoinAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<QuestJoinResult>.Fail(400, "A valid quest ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<QuestJoinResult>.Fail(400, "A valid user ID is required.");
        }

        var quest = await dbContext.Quests
            .AsNoTracking()
            .FirstOrDefaultAsync(quest => quest.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<QuestJoinResult>.Fail(404, "Quest was not found.");
        }

        var existing = await dbContext.QuestParticipations
            .FirstOrDefaultAsync(
                participation => participation.QuestId == questId && participation.UserId == userId,
                cancellationToken);

        var isAvailable = IsAvailable(quest, DateTime.UtcNow);

        if (existing is not null)
        {
            // Доступность квеста проверяется и для повторного входа: иначе повторный join
            // истёкшего квеста возвращал бы 200, тогда как шаги и завершение уже дают 409.
            return existing.Status switch
            {
                QuestParticipationStatuses.InProgress when isAvailable =>
                    ServiceResult<QuestJoinResult>.Ok(new QuestJoinResult(existing, true)),
                QuestParticipationStatuses.InProgress =>
                    ServiceResult<QuestJoinResult>.Fail(
                        409,
                        "Quest is not active or outside its date range."),
                QuestParticipationStatuses.Completed =>
                    ServiceResult<QuestJoinResult>.Fail(409, "Quest is already completed."),
                _ => ServiceResult<QuestJoinResult>.Fail(
                    409,
                    "Quest participation was cancelled and cannot be resumed.")
            };
        }

        if (!isAvailable)
        {
            return ServiceResult<QuestJoinResult>.Fail(
                409,
                "Quest is not active or outside its date range.");
        }

        var now = DateTime.UtcNow;

        var participation = new QuestParticipation
        {
            QuestId = questId,
            UserId = userId,
            Status = QuestParticipationStatuses.InProgress,
            StartedAt = now,
            CreatedAt = now
        };

        dbContext.QuestParticipations.Add(participation);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(participation).State = EntityState.Detached;

            var concurrent = await dbContext.QuestParticipations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.QuestId == questId && item.UserId == userId,
                    cancellationToken);

            if (concurrent is null)
            {
                return ServiceResult<QuestJoinResult>.Fail(
                    409,
                    "The request conflicted with another update. Please retry.");
            }

            return concurrent.Status switch
            {
                QuestParticipationStatuses.InProgress =>
                    ServiceResult<QuestJoinResult>.Ok(new QuestJoinResult(concurrent, true)),
                QuestParticipationStatuses.Completed =>
                    ServiceResult<QuestJoinResult>.Fail(409, "Quest is already completed."),
                _ => ServiceResult<QuestJoinResult>.Fail(
                    409,
                    "Quest participation was cancelled and cannot be resumed.")
            };
        }

        return ServiceResult<QuestJoinResult>.Ok(new QuestJoinResult(participation, false));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestStepResult>> CompleteStepAsync(
        long questId,
        long stepId,
        Guid userId,
        CompleteQuestStepCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<QuestStepResult>.Fail(400, "A completion command is required.");
        }

        if (questId <= 0 || stepId <= 0)
        {
            return ServiceResult<QuestStepResult>.Fail(400, "Valid quest and step IDs are required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<QuestStepResult>.Fail(400, "A valid user ID is required.");
        }

        var quest = await dbContext.Quests
            .AsNoTracking()
            .FirstOrDefaultAsync(quest => quest.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<QuestStepResult>.Fail(404, "Quest was not found.");
        }

        var step = await dbContext.QuestSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                step => step.Id == stepId && step.QuestId == questId,
                cancellationToken);

        if (step is null)
        {
            return ServiceResult<QuestStepResult>.Fail(404, "Quest step was not found.");
        }

        var participation = await dbContext.QuestParticipations
            .FirstOrDefaultAsync(
                participation => participation.QuestId == questId && participation.UserId == userId,
                cancellationToken);

        if (participation is null)
        {
            return ServiceResult<QuestStepResult>.Fail(404, "You have not joined this quest.");
        }

        if (participation.Status == QuestParticipationStatuses.Completed)
        {
            return ServiceResult<QuestStepResult>.Fail(409, "Quest is already completed.");
        }

        if (participation.Status != QuestParticipationStatuses.InProgress)
        {
            return ServiceResult<QuestStepResult>.Fail(409, "Quest participation is not active.");
        }

        if (!IsAvailable(quest, DateTime.UtcNow))
        {
            return ServiceResult<QuestStepResult>.Fail(
                409,
                "Quest is not active or outside its date range.");
        }

        if (step.CompletionType == QuestStepCompletionType.Automatic)
        {
            return ServiceResult<QuestStepResult>.Fail(
                409,
                $"Step \"{step.Title}\" requires automatic completion, which is not supported yet.");
        }

        var completionType = QuestStepCompletionTypeNames.ToDbValue(step.CompletionType);
        var storagePath = command.StoragePath?.Trim();
        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();

        if (step.CompletionType == QuestStepCompletionType.Proof && string.IsNullOrEmpty(storagePath))
        {
            return ServiceResult<QuestStepResult>.Fail(
                400,
                "StoragePath is required for a proof step.");
        }

        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var completion = await dbContext.QuestStepCompletions
                .Include(completion => completion.QuestProofs)
                .FirstOrDefaultAsync(
                    completion => completion.ParticipationId == participation.Id
                                  && completion.StepId == step.Id,
                    cancellationToken);

            if (step.CompletionType == QuestStepCompletionType.Manual)
            {
                if (completion is not null)
                {
                    return ServiceResult<QuestStepResult>.Ok(
                        new QuestStepResult(completion, completionType, true, null, true));
                }

                completion = new QuestStepCompletion
                {
                    ParticipationId = participation.Id,
                    QuestId = questId,
                    StepId = step.Id,
                    CompletedAt = DateTime.UtcNow
                };

                dbContext.QuestStepCompletions.Add(completion);

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await databaseTransaction.CommitAsync(cancellationToken);

                    return ServiceResult<QuestStepResult>.Ok(
                        new QuestStepResult(completion, completionType, true, null, false));
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    // Конкурентный запрос уже создал завершение этого же шага. Строка к моменту
                    // ошибки уникальности зафиксирована, поэтому запрос возвращает успех победителя
                    // (идемпотентный результат), а не 409.
                    await databaseTransaction.RollbackAsync(cancellationToken);
                    dbContext.Entry(completion).State = EntityState.Detached;

                    var concurrent = await dbContext.QuestStepCompletions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            item => item.ParticipationId == participation.Id && item.StepId == step.Id,
                            cancellationToken);

                    if (concurrent is not null)
                    {
                        return ServiceResult<QuestStepResult>.Ok(
                            new QuestStepResult(concurrent, completionType, true, null, true));
                    }

                    return ServiceResult<QuestStepResult>.Fail(
                        409,
                        "The step completion conflicted with another request. Please retry.");
                }
            }

            var isNewCompletion = completion is null;

            completion ??= new QuestStepCompletion
            {
                ParticipationId = participation.Id,
                QuestId = questId,
                StepId = step.Id,
                CompletedAt = DateTime.UtcNow
            };

            var latestProof = isNewCompletion ? null : LatestProof(completion);

            if (latestProof?.Status == QuestProofStatuses.Pending)
            {
                return ServiceResult<QuestStepResult>.Fail(
                    409,
                    "The proof is already awaiting review.");
            }

            if (latestProof?.Status == QuestProofStatuses.Approved)
            {
                return ServiceResult<QuestStepResult>.Ok(
                    new QuestStepResult(
                        completion,
                        completionType,
                        true,
                        QuestProofStatuses.Approved,
                        true));
            }

            completion.QuestProofs.Add(new QuestProof
            {
                StoragePath = storagePath!,
                Comment = comment,
                Status = QuestProofStatuses.Pending,
                CreatedAt = DateTime.UtcNow
            });

            if (isNewCompletion)
            {
                dbContext.QuestStepCompletions.Add(completion);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);

            return ServiceResult<QuestStepResult>.Ok(
                new QuestStepResult(
                    completion,
                    completionType,
                    false,
                    QuestProofStatuses.Pending,
                    false));
        }
        catch (Exception exception) when (IsRetryableConflict(exception) || IsUniqueViolation(exception))
        {
            return ServiceResult<QuestStepResult>.Fail(
                409,
                "The request conflicted with another update. Please retry.");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestCompletionResult>> CompleteAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<QuestCompletionResult>.Fail(400, "A valid quest ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<QuestCompletionResult>.Fail(400, "A valid user ID is required.");
        }

        var quest = await dbContext.Quests
            .AsNoTracking()
            .Include(quest => quest.QuestSteps)
            .FirstOrDefaultAsync(quest => quest.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<QuestCompletionResult>.Fail(404, "Quest was not found.");
        }

        var participation = await dbContext.QuestParticipations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                participation => participation.QuestId == questId && participation.UserId == userId,
                cancellationToken);

        if (participation is null)
        {
            return ServiceResult<QuestCompletionResult>.Fail(404, "You have not joined this quest.");
        }

        if (participation.Status == QuestParticipationStatuses.Completed)
        {
            return await CompleteAlreadyCompletedAsync(participation, cancellationToken);
        }

        if (participation.Status != QuestParticipationStatuses.InProgress)
        {
            return ServiceResult<QuestCompletionResult>.Fail(409, "Quest participation is not active.");
        }

        if (!IsAvailable(quest, DateTime.UtcNow))
        {
            return ServiceResult<QuestCompletionResult>.Fail(
                409,
                "Quest is not active or outside its date range.");
        }

        var requiredSteps = quest.QuestSteps
            .Where(step => step.IsRequired)
            .OrderBy(step => step.SortOrder)
            .ThenBy(step => step.Id)
            .ToList();

        if (requiredSteps.Count == 0)
        {
            return ServiceResult<QuestCompletionResult>.Fail(
                409,
                "The quest has no mandatory steps configured.");
        }

        var automaticSteps = requiredSteps
            .Where(step => step.CompletionType == QuestStepCompletionType.Automatic)
            .ToList();

        if (automaticSteps.Count > 0)
        {
            return ServiceResult<QuestCompletionResult>.Fail(
                409,
                $"Mandatory steps require automatic completion, which is not supported yet: {FormatTitles(automaticSteps)}.");
        }

        var completions = await dbContext.QuestStepCompletions
            .AsNoTracking()
            .Where(completion => completion.ParticipationId == participation.Id)
            .Include(completion => completion.QuestProofs)
            .ToListAsync(cancellationToken);

        var missing = new List<QuestStep>();
        var awaitingReview = new List<QuestStep>();
        var rejected = new List<QuestStep>();

        foreach (var step in requiredSteps)
        {
            var stepCompletions = completions
                .Where(completion => completion.StepId == step.Id)
                .ToList();

            if (step.CompletionType == QuestStepCompletionType.Manual)
            {
                if (stepCompletions.Count == 0)
                {
                    missing.Add(step);
                }

                continue;
            }

            var stepCompletion = stepCompletions
                .OrderByDescending(completion => completion.CompletedAt)
                .ThenByDescending(completion => completion.Id)
                .FirstOrDefault();

            var proof = stepCompletion is null ? null : LatestProof(stepCompletion);

            switch (proof?.Status)
            {
                case QuestProofStatuses.Approved:
                    break;
                case QuestProofStatuses.Pending:
                    awaitingReview.Add(step);
                    break;
                case QuestProofStatuses.Rejected:
                    rejected.Add(step);
                    break;
                default:
                    missing.Add(step);
                    break;
            }
        }

        var errors = new List<string>();

        if (missing.Count > 0)
        {
            errors.Add($"Mandatory steps are not completed: {FormatTitles(missing)}.");
        }

        if (awaitingReview.Count > 0)
        {
            errors.Add($"Proofs are awaiting review for steps: {FormatTitles(awaitingReview)}.");
        }

        if (rejected.Count > 0)
        {
            errors.Add(
                $"Proofs were rejected for steps: {FormatTitles(rejected)}. Submit new proofs and try again.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult<QuestCompletionResult>.Fail(409, string.Join(" ", errors));
        }

        var completedAt = DateTime.UtcNow;

        await using (var databaseTransaction = await dbContext.Database
                         .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            try
            {
                // Условный UPDATE переводит участие в completed только из in_progress,
                // поэтому инвариант перехода не зависит от уровня изоляции: проигравший
                // конкурент получает 0 строк вместо повторного завершения.
                var transitioned = await dbContext.QuestParticipations
                    .Where(item => item.Id == participation.Id
                                   && item.Status == QuestParticipationStatuses.InProgress)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.Status, QuestParticipationStatuses.Completed)
                            .SetProperty(item => item.CompletedAt, (DateTime?)completedAt),
                        cancellationToken);

                if (transitioned == 0)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);
                }
                else
                {
                    // Награда (включая нулевую) начисляется в той же транзакции, что и переход
                    // статуса: сумма читается внутри транзакции, поэтому решение не опирается
                    // на устаревший снимок.
                    var reward = await activityRewardService.GrantQuestRewardAsync(
                        participation.Id,
                        cancellationToken);

                    if (!reward.IsSuccess)
                    {
                        return ServiceResult<QuestCompletionResult>.Fail(
                            reward.StatusCode,
                            reward.Error!);
                    }

                    var grantedAmount = reward.Value!.AlreadyGranted ? 0 : reward.Value!.Amount;
                    var balanceAfter = reward.Value!.BalanceAfter
                        ?? await GetPersonalBalanceAsync(userId, cancellationToken);

                    participation.Status = QuestParticipationStatuses.Completed;
                    participation.CompletedAt = completedAt;
                    participation.RewardClaimed = true;

                    await databaseTransaction.CommitAsync(cancellationToken);

                    return ServiceResult<QuestCompletionResult>.Ok(new QuestCompletionResult(
                        participation,
                        reward.Value!.AlreadyGranted ? quest.RewardAmount : grantedAmount,
                        balanceAfter,
                        grantedAmount > 0,
                        false));
                }
            }
            catch (Exception exception) when (IsRetryableConflict(exception) || IsUniqueViolation(exception))
            {
                return ServiceResult<QuestCompletionResult>.Fail(
                    409,
                    "The quest completion conflicted with another request. Please retry.");
            }
        }

        // Переход не выполнен: участие уже завершено или отменено конкурентным запросом.
        var current = await dbContext.QuestParticipations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.QuestId == questId && item.UserId == userId,
                cancellationToken);

        if (current is null)
        {
            return ServiceResult<QuestCompletionResult>.Fail(404, "You have not joined this quest.");
        }

        return current.Status == QuestParticipationStatuses.Completed
            ? await CompleteAlreadyCompletedAsync(current, cancellationToken)
            : ServiceResult<QuestCompletionResult>.Fail(409, "Quest participation is not active.");
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminQuestSummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Quests.AsNoTracking();

        if (isActive is bool active)
        {
            query = query.Where(quest => quest.IsActive == active);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var quests = await query
            .OrderByDescending(quest => quest.CreatedAt)
            .ThenByDescending(quest => quest.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (quests.Count == 0)
        {
            return new PagedResult<AdminQuestSummary>([], totalCount, page, pageSize);
        }

        var questIds = quests.Select(quest => quest.Id).ToList();

        var stepsCounts = await dbContext.QuestSteps
            .AsNoTracking()
            .Where(step => questIds.Contains(step.QuestId))
            .GroupBy(step => step.QuestId)
            .Select(group => new { QuestId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.QuestId, row => row.Count, cancellationToken);

        var participantsCounts = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation => questIds.Contains(participation.QuestId))
            .GroupBy(participation => participation.QuestId)
            .Select(group => new { QuestId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.QuestId, row => row.Count, cancellationToken);

        var pendingProofsCounts = await dbContext.QuestProofs
            .AsNoTracking()
            .Where(proof => proof.Status == QuestProofStatuses.Pending)
            .Join(
                dbContext.QuestStepCompletions.AsNoTracking(),
                proof => proof.CompletionId,
                completion => completion.Id,
                (proof, completion) => completion.QuestId)
            .Where(questId => questIds.Contains(questId))
            .GroupBy(questId => questId)
            .Select(group => new { QuestId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.QuestId, row => row.Count, cancellationToken);

        var items = quests
            .Select(quest => new AdminQuestSummary(
                quest,
                stepsCounts.TryGetValue(quest.Id, out var stepsCount) ? stepsCount : 0,
                participantsCounts.TryGetValue(quest.Id, out var participantsCount)
                    ? participantsCount
                    : 0,
                pendingProofsCounts.TryGetValue(quest.Id, out var pendingProofsCount)
                    ? pendingProofsCount
                    : 0))
            .ToList();

        return new PagedResult<AdminQuestSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Quest>> CreateAsync(
        QuestCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<Quest>.Fail(400, "A quest command is required.");
        }

        if (createdBy == Guid.Empty)
        {
            return ServiceResult<Quest>.Fail(400, "A valid creator ID is required.");
        }

        var validationError = ValidateQuestFields(
            command.Title,
            command.RewardAmount,
            command.StartAt,
            command.EndAt);

        if (validationError is not null)
        {
            return ServiceResult<Quest>.Fail(400, validationError);
        }

        var now = DateTime.UtcNow;

        var quest = new Quest
        {
            Title = command.Title!.Trim(),
            Description = NormalizeOptional(command.Description),
            RewardAmount = command.RewardAmount!.Value,
            StartAt = ToUtc(command.StartAt),
            EndAt = ToUtc(command.EndAt),
            IsActive = command.IsActive,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Quests.Add(quest);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            dbContext.Entry(quest).State = EntityState.Detached;

            return ServiceResult<Quest>.Fail(400, "The quest creator was not found.");
        }

        return ServiceResult<Quest>.Ok(quest);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Quest>> UpdateAsync(
        long questId,
        QuestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<Quest>.Fail(400, "A valid quest ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<Quest>.Fail(400, "A quest command is required.");
        }

        var quest = await dbContext.Quests
            .FirstOrDefaultAsync(item => item.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<Quest>.Fail(404, "Quest was not found.");
        }

        var validationError = ValidateQuestFields(
            command.Title,
            command.RewardAmount,
            command.StartAt,
            command.EndAt);

        if (validationError is not null)
        {
            return ServiceResult<Quest>.Fail(400, validationError);
        }

        quest.Title = command.Title!.Trim();
        quest.Description = NormalizeOptional(command.Description);
        quest.RewardAmount = command.RewardAmount!.Value;
        quest.StartAt = ToUtc(command.StartAt);
        quest.EndAt = ToUtc(command.EndAt);
        quest.IsActive = command.IsActive;
        quest.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Quest>.Ok(quest);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Quest>> DeactivateAsync(
        long questId,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<Quest>.Fail(400, "A valid quest ID is required.");
        }

        var quest = await dbContext.Quests
            .FirstOrDefaultAsync(item => item.Id == questId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<Quest>.Fail(404, "Quest was not found.");
        }

        if (quest.IsActive)
        {
            // Квест не удаляется физически: шаги, участия и доказательства остаются
            // частью истории, поэтому удаление сводится к деактивации.
            quest.IsActive = false;
            quest.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<Quest>.Ok(quest);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestStep>> CreateStepAsync(
        long questId,
        QuestStepCommand command,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0)
        {
            return ServiceResult<QuestStep>.Fail(400, "A valid quest ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<QuestStep>.Fail(400, "A quest step command is required.");
        }

        var validationError = ValidateStepFields(command, out var completionType);

        if (validationError is not null)
        {
            return ServiceResult<QuestStep>.Fail(400, validationError);
        }

        var questExists = await dbContext.Quests
            .AsNoTracking()
            .AnyAsync(quest => quest.Id == questId, cancellationToken);

        if (!questExists)
        {
            return ServiceResult<QuestStep>.Fail(404, "Quest was not found.");
        }

        var step = new QuestStep
        {
            QuestId = questId,
            Title = command.Title!.Trim(),
            Description = NormalizeOptional(command.Description),
            CompletionType = completionType,
            SortOrder = command.SortOrder,
            IsRequired = command.IsRequired,
            CompletionData = NormalizeOptional(command.CompletionData),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.QuestSteps.Add(step);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            dbContext.Entry(step).State = EntityState.Detached;

            return ServiceResult<QuestStep>.Fail(404, "Quest was not found.");
        }

        return ServiceResult<QuestStep>.Ok(step);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestStep>> UpdateStepAsync(
        long questId,
        long stepId,
        QuestStepCommand command,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0 || stepId <= 0)
        {
            return ServiceResult<QuestStep>.Fail(
                400,
                "Valid quest and step IDs are required.");
        }

        if (command is null)
        {
            return ServiceResult<QuestStep>.Fail(400, "A quest step command is required.");
        }

        var validationError = ValidateStepFields(command, out var completionType);

        if (validationError is not null)
        {
            return ServiceResult<QuestStep>.Fail(400, validationError);
        }

        var step = await dbContext.QuestSteps
            .FirstOrDefaultAsync(
                item => item.Id == stepId && item.QuestId == questId,
                cancellationToken);

        if (step is null)
        {
            var questExists = await dbContext.Quests
                .AsNoTracking()
                .AnyAsync(quest => quest.Id == questId, cancellationToken);

            return questExists
                ? ServiceResult<QuestStep>.Fail(404, "Quest step was not found.")
                : ServiceResult<QuestStep>.Fail(404, "Quest was not found.");
        }

        if (step.CompletionType != completionType)
        {
            var hasCompletions = await dbContext.QuestStepCompletions
                .AsNoTracking()
                .AnyAsync(completion => completion.StepId == stepId, cancellationToken);

            if (hasCompletions)
            {
                return ServiceResult<QuestStep>.Fail(
                    409,
                    "The step completion type cannot be changed because participants have already completed this step.");
            }
        }

        step.Title = command.Title!.Trim();
        step.Description = NormalizeOptional(command.Description);
        step.CompletionType = completionType;
        step.SortOrder = command.SortOrder;
        step.IsRequired = command.IsRequired;
        step.CompletionData = NormalizeOptional(command.CompletionData);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<QuestStep>.Ok(step);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestStep>> DeleteStepAsync(
        long questId,
        long stepId,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0 || stepId <= 0)
        {
            return ServiceResult<QuestStep>.Fail(
                400,
                "Valid quest and step IDs are required.");
        }

        var step = await dbContext.QuestSteps
            .FirstOrDefaultAsync(
                item => item.Id == stepId && item.QuestId == questId,
                cancellationToken);

        if (step is null)
        {
            var questExists = await dbContext.Quests
                .AsNoTracking()
                .AnyAsync(quest => quest.Id == questId, cancellationToken);

            return questExists
                ? ServiceResult<QuestStep>.Fail(404, "Quest step was not found.")
                : ServiceResult<QuestStep>.Fail(404, "Quest was not found.");
        }

        var hasCompletions = await dbContext.QuestStepCompletions
            .AsNoTracking()
            .AnyAsync(completion => completion.StepId == stepId, cancellationToken);

        if (hasCompletions)
        {
            return ServiceResult<QuestStep>.Fail(
                409,
                "The step cannot be deleted because participants have already completed it.");
        }

        dbContext.QuestSteps.Remove(step);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            dbContext.Entry(step).State = EntityState.Detached;

            return ServiceResult<QuestStep>.Fail(
                409,
                "The step cannot be deleted because participants have already completed it.");
        }

        return ServiceResult<QuestStep>.Ok(step);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PagedResult<QuestProofView>>> GetProofsAsync(
        string? status,
        long? questId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (questId is <= 0)
        {
            return ServiceResult<PagedResult<QuestProofView>>.Fail(
                400,
                "A valid quest ID is required.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var hasStatusFilter = !string.IsNullOrWhiteSpace(status);
        var statusFilter = ParseProofStatus(status);

        if (hasStatusFilter && statusFilter is null)
        {
            return ServiceResult<PagedResult<QuestProofView>>.Fail(
                400,
                "Status must be one of \"pending\", \"approved\" or \"rejected\".");
        }

        var query = dbContext.QuestProofs.AsNoTracking();

        if (statusFilter is not null)
        {
            query = query.Where(proof => proof.Status == statusFilter);
        }

        if (questId is long filterQuestId)
        {
            query = query.Where(proof => proof.Completion.QuestId == filterQuestId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var proofs = await query
            // Ожидающие проверки доказательства показываются первыми: это очередь модерации.
            .OrderBy(proof => proof.Status == QuestProofStatuses.Pending ? 0 : 1)
            .ThenByDescending(proof => proof.CreatedAt)
            .ThenByDescending(proof => proof.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(proof => new QuestProofView(
                proof,
                proof.Completion.QuestId,
                proof.Completion.QuestStep.Quest.Title,
                proof.Completion.StepId,
                proof.Completion.QuestStep.Title,
                proof.Completion.QuestParticipation.UserId))
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResult<QuestProofView>>.Ok(
            new PagedResult<QuestProofView>(proofs, totalCount, page, pageSize));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<QuestProofReviewResult>> ReviewProofAsync(
        long proofId,
        ReviewQuestProofCommand command,
        Guid reviewedBy,
        CancellationToken cancellationToken = default)
    {
        if (proofId <= 0)
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                400,
                "A valid proof ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                400,
                "A proof review command is required.");
        }

        if (reviewedBy == Guid.Empty)
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                400,
                "A valid reviewer ID is required.");
        }

        var targetStatus = ParseProofStatus(command.Status);

        if (targetStatus is not (QuestProofStatuses.Approved or QuestProofStatuses.Rejected))
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                400,
                "Status must be either \"approved\" or \"rejected\".");
        }

        var currentStatus = await dbContext.QuestProofs
            .AsNoTracking()
            .Where(proof => proof.Id == proofId)
            .Select(proof => proof.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentStatus is null)
        {
            return ServiceResult<QuestProofReviewResult>.Fail(404, "Proof was not found.");
        }

        if (currentStatus == targetStatus)
        {
            var existing = await LoadProofViewAsync(proofId, cancellationToken);

            return existing is null
                ? ServiceResult<QuestProofReviewResult>.Fail(404, "Proof was not found.")
                : ServiceResult<QuestProofReviewResult>.Ok(
                    new QuestProofReviewResult(existing, true));
        }

        if (currentStatus != QuestProofStatuses.Pending)
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                409,
                "The proof has already been reviewed.");
        }

        try
        {
            // Условный UPDATE переводит доказательство из pending только один раз:
            // конкурентный проверяющий получает 0 строк и видит уже принятое решение.
            var updated = await dbContext.QuestProofs
                .Where(proof => proof.Id == proofId && proof.Status == QuestProofStatuses.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(proof => proof.Status, targetStatus)
                        .SetProperty(proof => proof.ReviewedBy, (Guid?)reviewedBy)
                        .SetProperty(proof => proof.ReviewedAt, (DateTime?)DateTime.UtcNow),
                    cancellationToken);

            var view = await LoadProofViewAsync(proofId, cancellationToken);

            if (view is null)
            {
                return ServiceResult<QuestProofReviewResult>.Fail(404, "Proof was not found.");
            }

            if (updated == 0)
            {
                return view.Proof.Status == targetStatus
                    ? ServiceResult<QuestProofReviewResult>.Ok(
                        new QuestProofReviewResult(view, true))
                    : ServiceResult<QuestProofReviewResult>.Fail(
                        409,
                        "The proof has already been reviewed.");
            }

            return ServiceResult<QuestProofReviewResult>.Ok(
                new QuestProofReviewResult(view, false));
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            return ServiceResult<QuestProofReviewResult>.Fail(
                409,
                "The proof review conflicted with another request. Please retry.");
        }
    }

    private static bool IsAvailable(Quest quest, DateTime now) =>
        quest.IsActive
        && (quest.StartAt is null || quest.StartAt <= now)
        && (quest.EndAt is null || quest.EndAt >= now);

    private static bool IsStepSatisfied(
        QuestStep step,
        IEnumerable<QuestStepCompletion> completions) =>
        step.CompletionType switch
        {
            QuestStepCompletionType.Manual => completions
                .Any(completion => completion.StepId == step.Id),
            QuestStepCompletionType.Proof => completions
                .Where(completion => completion.StepId == step.Id)
                .Select(LatestProof)
                .Any(proof => proof?.Status == QuestProofStatuses.Approved),
            _ => false
        };

    private static QuestProof? LatestProof(QuestStepCompletion completion) =>
        completion.QuestProofs
            .OrderByDescending(proof => proof.CreatedAt)
            .ThenByDescending(proof => proof.Id)
            .FirstOrDefault();

    private static string FormatTitles(IEnumerable<QuestStep> steps) =>
        string.Join(", ", steps.Select(step => $"\"{step.Title}\""));

    /// <summary>
    /// Проверяет поля квеста и возвращает сообщение об ошибке или <c>null</c>.
    /// </summary>
    private static string? ValidateQuestFields(
        string? title,
        long? rewardAmount,
        DateTime? startAt,
        DateTime? endAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }

        if (title.Trim().Length > TitleMaxLength)
        {
            return $"Title must not exceed {TitleMaxLength} characters.";
        }

        if (rewardAmount is null)
        {
            return "RewardAmount is required.";
        }

        if (rewardAmount < 0)
        {
            return "RewardAmount cannot be negative.";
        }

        if (startAt is not null && endAt is not null && endAt <= startAt)
        {
            return "EndAt must be later than StartAt.";
        }

        return null;
    }

    /// <summary>
    /// Проверяет поля шага квеста и возвращает сообщение об ошибке или <c>null</c>.
    /// </summary>
    private static string? ValidateStepFields(
        QuestStepCommand command,
        out QuestStepCompletionType completionType)
    {
        completionType = QuestStepCompletionType.Manual;

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return "Title is required.";
        }

        if (command.Title.Trim().Length > TitleMaxLength)
        {
            return $"Title must not exceed {TitleMaxLength} characters.";
        }

        if (command.SortOrder < 0)
        {
            return "SortOrder cannot be negative.";
        }

        var completionTypeValue = command.CompletionType?.Trim().ToLowerInvariant();

        if (completionTypeValue is not null
            && !QuestStepCompletionTypeNames.TryParse(completionTypeValue, out completionType))
        {
            return "CompletionType must be one of \"manual\", \"proof\" or \"automatic\".";
        }

        if (!string.IsNullOrWhiteSpace(command.CompletionData)
            && !IsValidJson(command.CompletionData))
        {
            return "CompletionData must be valid JSON.";
        }

        return null;
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ParseProofStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            QuestProofStatuses.Pending => QuestProofStatuses.Pending,
            QuestProofStatuses.Approved => QuestProofStatuses.Approved,
            QuestProofStatuses.Rejected => QuestProofStatuses.Rejected,
            _ => null
        };

    private Task<QuestProofView?> LoadProofViewAsync(
        long proofId,
        CancellationToken cancellationToken) =>
        dbContext.QuestProofs
            .AsNoTracking()
            .Where(proof => proof.Id == proofId)
            .Select(proof => new QuestProofView(
                proof,
                proof.Completion.QuestId,
                proof.Completion.QuestStep.Quest.Title,
                proof.Completion.StepId,
                proof.Completion.QuestStep.Title,
                proof.Completion.QuestParticipation.UserId))
            .FirstOrDefaultAsync(cancellationToken);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Приводит дату к UTC, чтобы значение можно было записать в <c>timestamptz</c>.
    /// </summary>
    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        null => null,
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    private static bool IsRetryableConflict(Exception exception) =>
        exception switch
        {
            DbUpdateException updateException =>
                updateException.InnerException is not null
                && IsRetryableConflict(updateException.InnerException),
            PostgresException postgresException => postgresException.SqlState
                is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected
                or PostgresErrorCodes.LockNotAvailable,
            _ => false
        };

    private static bool IsUniqueViolation(Exception exception) =>
        exception switch
        {
            DbUpdateException updateException =>
                updateException.InnerException is not null
                && IsUniqueViolation(updateException.InnerException),
            PostgresException postgresException =>
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            _ => false
        };

    /// <summary>
    /// Обрабатывает запрос завершения для уже завершённого участия.
    /// Повторный запрос идемпотентен, но служит явным путём восстановления: если участие
    /// завершено, а награда не зафиксирована (<c>reward_claimed = false</c>), она выдаётся
    /// тем же атомарным захватом флага, поэтому два конкурентных восстановления не могут
    /// начислить награду дважды.
    /// </summary>
    private async Task<ServiceResult<QuestCompletionResult>> CompleteAlreadyCompletedAsync(
        QuestParticipation participation,
        CancellationToken cancellationToken)
    {
        var quest = await dbContext.Quests
            .AsNoTracking()
            .FirstOrDefaultAsync(quest => quest.Id == participation.QuestId, cancellationToken);

        if (quest is null)
        {
            return ServiceResult<QuestCompletionResult>.Fail(404, "Quest was not found.");
        }

        if (participation.RewardClaimed)
        {
            var balance = await GetPersonalBalanceAsync(participation.UserId, cancellationToken);

            return ServiceResult<QuestCompletionResult>.Ok(new QuestCompletionResult(
                participation,
                quest.RewardAmount,
                balance,
                false,
                true));
        }

        ActivityRewardResult reward;

        try
        {
            var rewardResult = await activityRewardService.GrantQuestRewardAsync(
                participation.Id,
                cancellationToken);

            if (!rewardResult.IsSuccess)
            {
                return ServiceResult<QuestCompletionResult>.Fail(
                    rewardResult.StatusCode,
                    rewardResult.Error!);
            }

            reward = rewardResult.Value!;
        }
        catch (Exception exception) when (IsRetryableConflict(exception) || IsUniqueViolation(exception))
        {
            return ServiceResult<QuestCompletionResult>.Fail(
                409,
                "The reward claim conflicted with another request. Please retry.");
        }

        participation.RewardClaimed = true;

        var currentBalance = reward.BalanceAfter
            ?? await GetPersonalBalanceAsync(participation.UserId, cancellationToken);

        return ServiceResult<QuestCompletionResult>.Ok(new QuestCompletionResult(
            participation,
            reward.AlreadyGranted ? quest.RewardAmount : reward.Amount,
            currentBalance,
            !reward.AlreadyGranted && reward.Amount > 0,
            true));
    }

    private Task<long?> GetPersonalBalanceAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Balances
            .AsNoTracking()
            .Where(balance => balance.UserId == userId && balance.Type == BalanceType.Personal)
            .Select(balance => (long?)balance.Amount)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceResult<QuestStepResult>> UpdateProofAsync(
        long questId,
        long stepId,
        Guid userId,
        CompleteQuestStepCommand command,
        CancellationToken cancellationToken = default)
    {
        if (questId <= 0 || stepId <= 0)
        {
            return ServiceResult<QuestStepResult>.Fail(
                400, "Valid quest and step IDs are required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<QuestStepResult>.Fail(
                400, "A valid user ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<QuestStepResult>.Fail(
                400, "A completion command is required.");
        }

        var storagePath = command.StoragePath?.Trim();
        var comment = string.IsNullOrWhiteSpace(command.Comment)
            ? null
            : command.Comment.Trim();

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return ServiceResult<QuestStepResult>.Fail(
                400, "StoragePath is required for a proof step.");
        }

        var participation = await dbContext.QuestParticipations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.QuestId == questId && p.UserId == userId,
                cancellationToken);

        if (participation is null)
        {
            return ServiceResult<QuestStepResult>.Fail(
                404, "You have not joined this quest.");
        }

        if (participation.Status != QuestParticipationStatuses.InProgress)
        {
            return ServiceResult<QuestStepResult>.Fail(
                409, "Quest participation is not active.");
        }

        var step = await dbContext.QuestSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == stepId
                     && s.QuestId == questId
                     && s.CompletionType == QuestStepCompletionType.Proof,
                cancellationToken);

        if (step is null)
        {
            return ServiceResult<QuestStepResult>.Fail(
                404, "Proof step was not found.");
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var completion = await dbContext.QuestStepCompletions
                .Include(c => c.QuestProofs)
                .FirstOrDefaultAsync(
                    c => c.ParticipationId == participation.Id
                         && c.StepId == stepId,
                    cancellationToken);

            if (completion is null)
            {
                return ServiceResult<QuestStepResult>.Fail(
                    404, "Proof submission was not found.");
            }

            var latestProof = LatestProof(completion);

            if (latestProof is null)
            {
                return ServiceResult<QuestStepResult>.Fail(
                    404, "Proof submission was not found.");
            }

            if (latestProof.Status != QuestProofStatuses.Pending)
            {
                return ServiceResult<QuestStepResult>.Fail(
                    409,
                    "Only proof submissions awaiting review can be edited.");
            }

            latestProof.StoragePath = storagePath;
            latestProof.Comment = comment;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<QuestStepResult>.Ok(
                new QuestStepResult(
                    completion,
                    QuestStepCompletionTypeNames.ToDbValue(step.CompletionType),
                    false,
                    QuestProofStatuses.Pending,
                    false));
        }
        catch (Exception exception)
            when (IsRetryableConflict(exception) || IsUniqueViolation(exception))
        {
            return ServiceResult<QuestStepResult>.Fail(
                409,
                "The proof update conflicted with another request. Please retry.");
        }
    }
}
