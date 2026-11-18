using System.Text.Json;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services.Quests;

/// <summary>
/// Запрос серверной проверки одного автоматического шага.
/// </summary>
/// <param name="ParticipationId">Идентификатор участия.</param>
/// <param name="StepId">Идентификатор шага.</param>
/// <param name="QuestId">Идентификатор квеста.</param>
/// <param name="StartedAt">Момент присоединения к квесту.</param>
/// <param name="Configuration">Типизированная конфигурация шага.</param>
public sealed record QuestStepVerificationRequest(
    long ParticipationId,
    long StepId,
    long QuestId,
    DateTime StartedAt,
    QuestStepActionConfiguration Configuration);

/// <summary>
/// Результат серверной проверки шага.
/// </summary>
/// <param name="IsSatisfied">Признак выполнения условия.</param>
/// <param name="EvidenceJson">Снимок источника данных для аудита.</param>
/// <param name="Reason">Причина, по которой условие не выполнено.</param>
public sealed record QuestStepVerification(
    bool IsSatisfied,
    string? EvidenceJson = null,
    string? Reason = null);

/// <summary>
/// Проверяет автоматические шаги по фактическим записям базы данных.
/// </summary>
public interface IQuestStepVerifier
{
    /// <summary>
    /// Проверяет пакет шагов. Запросы выполняются по одному на тип действия,
    /// а не по одному на шаг.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="requests">Пакет запросов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<IReadOnlyList<QuestStepVerification>> VerifyAsync(
        Guid userId,
        IReadOnlyList<QuestStepVerificationRequest> requests,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class QuestStepVerifier(AppDbContext dbContext) : IQuestStepVerifier
{
    private static readonly JsonSerializerOptions EvidenceOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestStepVerification>> VerifyAsync(
        Guid userId,
        IReadOnlyList<QuestStepVerificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return requests
                .Select(_ => new QuestStepVerification(false, null, "A valid user ID is required."))
                .ToList();
        }

        if (requests.Count == 0)
        {
            return [];
        }

        var results = new QuestStepVerification?[requests.Count];

        var groups = requests
            .Select((request, index) => new IndexedRequest(index, request))
            .GroupBy(
                item => item.Request.Configuration.Type,
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var items = group.ToList();

            switch (items[0].Request.Configuration.Type.ToLowerInvariant())
            {
                case QuestStepActionCodes.CompleteQuest:
                    await VerifyCompleteQuestAsync(userId, items, results, cancellationToken);
                    break;

                case QuestStepActionCodes.CompleteSurvey:
                    await VerifyCompleteSurveyAsync(userId, items, results, cancellationToken);
                    break;

                case QuestStepActionCodes.SendThanks:
                    await VerifyThanksAsync(userId, items, results, receive: false, cancellationToken);
                    break;

                case QuestStepActionCodes.ReceiveThanks:
                    await VerifyThanksAsync(userId, items, results, receive: true, cancellationToken);
                    break;

                case QuestStepActionCodes.MakePurchase:
                    await VerifyPurchaseAsync(userId, items, results, cancellationToken);
                    break;

                case QuestStepActionCodes.AttendEvent:
                    await VerifyAttendEventAsync(userId, items, results, cancellationToken);
                    break;

                case QuestStepActionCodes.CompleteProfile:
                    await VerifyCompleteProfileAsync(userId, items, results, cancellationToken);
                    break;

                case QuestStepActionCodes.EarnAchievement:
                    await VerifyEarnAchievementAsync(userId, items, results, cancellationToken);
                    break;

                default:
                    // Включая read_company_news: достоверного источника данных нет.
                    foreach (var item in items)
                    {
                        results[item.Index] = new QuestStepVerification(
                            false,
                            null,
                            $"Automatic step type \"{item.Request.Configuration.Type}\" has no verification source.");
                    }

                    break;
            }
        }

        return results
            .Select(result => result ?? new QuestStepVerification(
                false,
                null,
                "Automatic step was not verified."))
            .ToList();
    }

    private async Task VerifyCompleteQuestAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var targetIds = TargetIds(items);

        if (targetIds.Count == 0)
        {
            return;
        }

        var rows = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.UserId == userId
                && targetIds.Contains(participation.QuestId)
                && participation.Status == QuestParticipationStatuses.Completed)
            .Select(participation => new
            {
                participation.QuestId,
                participation.CompletedAt
            })
            .ToListAsync(cancellationToken);

        var completedAtByQuest = rows
            .Where(row => row.CompletedAt is not null)
            .GroupBy(row => row.QuestId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(row => row.CompletedAt!.Value));

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;

            if (!completedAtByQuest.TryGetValue(configuration.TargetId!.Value, out var completedAt))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The selected quest has not been completed by this user.");
                continue;
            }

            if (completedAt < item.Request.StartedAt)
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The selected quest was completed before the employee joined this quest.");
                continue;
            }

            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "quest_participation", configuration.TargetId, completedAt));
        }
    }

    private async Task VerifyCompleteSurveyAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var targetIds = TargetIds(items);

        if (targetIds.Count == 0)
        {
            return;
        }

        var surveys = await dbContext.Surveys
            .AsNoTracking()
            .Where(survey => targetIds.Contains(survey.Id))
            .Select(survey => new
            {
                survey.Id,
                survey.AllowMultipleResponses
            })
            .ToListAsync(cancellationToken);

        var surveyById = surveys.ToDictionary(survey => survey.Id);

        var responses = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response =>
                response.UserId == userId
                && targetIds.Contains(response.SurveyId))
            .Select(response => new
            {
                response.SurveyId,
                response.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        var lastBySurvey = responses
            .GroupBy(response => response.SurveyId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(response => response.SubmittedAt));

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;
            var targetId = configuration.TargetId!.Value;

            if (!surveyById.TryGetValue(targetId, out var survey))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The selected survey no longer exists.");
                continue;
            }

            if (!lastBySurvey.TryGetValue(targetId, out var submittedAt))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The employee has not submitted a response to the selected survey.");
                continue;
            }

            // Повторные ответы разрешены — ответ должен быть новым; иначе засчитывается
            // уже существующий ответ, который сотрудник не может повторить.
            if (survey.AllowMultipleResponses && submittedAt < item.Request.StartedAt)
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The survey response was submitted before the employee joined this quest.");
                continue;
            }

            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "survey_response", targetId, submittedAt));
        }
    }

    private async Task VerifyThanksAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        bool receive,
        CancellationToken cancellationToken)
    {
        var minStartedAt = items.Min(item => item.Request.StartedAt);

        var query = dbContext.Thanks
            .AsNoTracking()
            .Where(thank => thank.CreatedAt >= minStartedAt);

        query = receive
            ? query.Where(thank => thank.ToUserId == userId && thank.FromUserId != userId)
            : query.Where(thank => thank.FromUserId == userId);

        var timestamps = await query
            .Select(thank => thank.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;
            var required = configuration.Count ?? 1;
            var matching = timestamps
                .Where(createdAt => createdAt >= item.Request.StartedAt)
                .OrderBy(createdAt => createdAt)
                .ToList();

            if (matching.Count < required)
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    receive
                        ? $"The employee received {matching.Count} thank(s); {required} required."
                        : $"The employee sent {matching.Count} thank(s); {required} required.");
                continue;
            }

            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(
                    configuration.Type,
                    "thank",
                    null,
                    matching[required - 1],
                    ("count", matching.Count)));
        }
    }

    private async Task VerifyPurchaseAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var minStartedAt = items.Min(item => item.Request.StartedAt);

        // Отменённые и возвращённые заказы не учитываются.
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.UserId == userId
                && order.CreatedAt >= minStartedAt
                && (order.Status == OrderStatus.Paid
                    || order.Status == OrderStatus.Processing
                    || order.Status == OrderStatus.Ready
                    || order.Status == OrderStatus.Delivered))
            .Select(order => new
            {
                order.Id,
                order.TotalAmount,
                order.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(order => order.Id).ToList();
        var itemsByOrder = new Dictionary<long, HashSet<long>>();

        if (orderIds.Count > 0)
        {
            var orderItems = await dbContext.OrderItems
                .AsNoTracking()
                .Where(item => orderIds.Contains(item.OrderId) && item.ProductId != null)
                .Select(item => new { item.OrderId, item.ProductId })
                .ToListAsync(cancellationToken);

            foreach (var orderItem in orderItems)
            {
                if (!itemsByOrder.TryGetValue(orderItem.OrderId, out var productIds))
                {
                    productIds = [];
                    itemsByOrder[orderItem.OrderId] = productIds;
                }

                productIds.Add(orderItem.ProductId!.Value);
            }
        }

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;

            var matching = orders
                .Where(order =>
                    order.CreatedAt >= item.Request.StartedAt
                    && (configuration.MinAmount is not long minAmount || order.TotalAmount >= minAmount)
                    && (configuration.ProductId is not long productId
                        || (itemsByOrder.TryGetValue(order.Id, out var productIds)
                            && productIds.Contains(productId))))
                .OrderByDescending(order => order.CreatedAt)
                .FirstOrDefault();

            if (matching is null)
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "No paid order matching the configured amount or product was found after the employee joined.");
                continue;
            }

            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "order", matching.Id, matching.CreatedAt));
        }
    }

    private async Task VerifyAttendEventAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var targetIds = TargetIds(items);

        if (targetIds.Count == 0)
        {
            return;
        }

        var rows = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.UserId == userId
                && targetIds.Contains(participant.EventId)
                && participant.Status == EventParticipantStatuses.Attended)
            .Select(participant => new
            {
                participant.EventId,
                participant.AttendedAt
            })
            .ToListAsync(cancellationToken);

        var attendedByEvent = rows
            .Where(row => row.AttendedAt is not null)
            .GroupBy(row => row.EventId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(row => row.AttendedAt!.Value));

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;
            var targetId = configuration.TargetId!.Value;

            if (!attendedByEvent.TryGetValue(targetId, out var attendedAt))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The employee's attendance at the selected event is not confirmed.");
                continue;
            }

            // Посещение подтверждает администратор, поэтому это устойчивый факт:
            // отметка может быть сделана как до, так и после присоединения к квесту.
            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "event_participant", targetId, attendedAt));
        }
    }

    private async Task VerifyCompleteProfileAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var avatarUrl = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.AvatarUrl)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;
            var requireAvatar = configuration.RequireAvatar ?? true;

            if (requireAvatar && string.IsNullOrWhiteSpace(avatarUrl))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    "The employee has no avatar in the profile.");
                continue;
            }

            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "user", null, null));
        }
    }

    private async Task VerifyEarnAchievementAsync(
        Guid userId,
        IReadOnlyList<IndexedRequest> items,
        QuestStepVerification?[] results,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.UserAchievements
            .AsNoTracking()
            .Where(userAchievement => userAchievement.UserId == userId)
            .Select(userAchievement => new
            {
                userAchievement.Achievement.Code,
                userAchievement.EarnedAt
            })
            .ToListAsync(cancellationToken);

        var earnedByCode = rows
            .GroupBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key.Trim(),
                group => group.Max(row => row.EarnedAt),
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var configuration = item.Request.Configuration;
            var code = configuration.AchievementCode!.Trim();

            if (!earnedByCode.TryGetValue(code, out var earnedAt))
            {
                results[item.Index] = new QuestStepVerification(
                    false,
                    null,
                    $"The employee has not earned achievement \"{code}\".");
                continue;
            }

            // Достижения одноразовые, повторно их получить нельзя: учитывается
            // уже полученное достижение.
            results[item.Index] = new QuestStepVerification(
                true,
                Evidence(configuration.Type, "achievement", null, earnedAt, ("code", code)));
        }
    }

    private static List<long> TargetIds(IReadOnlyList<IndexedRequest> items) =>
        items
            .Select(item => item.Request.Configuration.TargetId)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

    private static string Evidence(
        string action,
        string entity,
        long? entityId,
        DateTime? occurredAt,
        params (string Key, object Value)[] extra)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = action,
            ["entity"] = entity,
            ["entityId"] = entityId,
            ["occurredAt"] = occurredAt,
            ["verifiedAt"] = DateTime.UtcNow
        };

        foreach (var (key, value) in extra)
        {
            payload[key] = value;
        }

        return JsonSerializer.Serialize(payload, EvidenceOptions);
    }

    private sealed record IndexedRequest(int Index, QuestStepVerificationRequest Request);
}
