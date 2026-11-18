using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services.Quests;

/// <summary>
/// Вариант выбора объекта для параметра автоматического шага.
/// </summary>
/// <param name="Id">Идентификатор объекта или код для достижения.</param>
/// <param name="Title">Название для администратора.</param>
/// <param name="Subtitle">Дополнительная подпись: срок, статус, цена.</param>
/// <param name="IsActive">Признак доступности объекта.</param>
public sealed record QuestStepActionOption(
    string Id,
    string Title,
    string? Subtitle,
    bool IsActive);

/// <summary>
/// Реальные объекты, которые Backend передаёт ИИ как единственный допустимый источник
/// идентификаторов. Клиент не может подменить этот список.
/// </summary>
/// <param name="Quests">Доступные квесты.</param>
/// <param name="Surveys">Доступные анкеты.</param>
/// <param name="Events">Предстоящие мероприятия.</param>
/// <param name="Achievements">Активные достижения.</param>
public sealed record QuestStepActionCandidateSets(
    IReadOnlyList<QuestStepActionOption> Quests,
    IReadOnlyList<QuestStepActionOption> Surveys,
    IReadOnlyList<QuestStepActionOption> Events,
    IReadOnlyList<QuestStepActionOption> Achievements);

/// <summary>
/// Источник вариантов для административных списков выбора и для ИИ.
/// </summary>
public interface IQuestStepActionOptionProvider
{
    /// <summary>
    /// Возвращает варианты для источника параметра (<c>quests</c>, <c>surveys</c>,
    /// <c>events</c>, <c>products</c>, <c>achievements</c>, <c>news</c>).
    /// </summary>
    /// <param name="optionsSource">Источник вариантов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<IReadOnlyList<QuestStepActionOption>> GetOptionsAsync(
        string optionsSource,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ограниченный набор реальных объектов для ИИ.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<QuestStepActionCandidateSets> GetCandidateSetsAsync(
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class QuestStepActionOptionProvider(AppDbContext dbContext) : IQuestStepActionOptionProvider
{
    /// <summary>Максимум вариантов в административном списке.</summary>
    public const int AdminOptionLimit = 100;

    /// <summary>Максимум вариантов, передаваемых ИИ, чтобы не раздувать промпт.</summary>
    public const int AiCandidateLimit = 20;

    /// <inheritdoc />
    public Task<IReadOnlyList<QuestStepActionOption>> GetOptionsAsync(
        string optionsSource,
        CancellationToken cancellationToken = default) =>
        optionsSource?.Trim().ToLowerInvariant() switch
        {
            "quests" => GetQuestsAsync(AdminOptionLimit, activeOnly: false, cancellationToken),
            "surveys" => GetSurveysAsync(AdminOptionLimit, activeOnly: false, cancellationToken),
            "events" => GetEventsAsync(AdminOptionLimit, activeOnly: false, cancellationToken),
            "products" => GetProductsAsync(AdminOptionLimit, activeOnly: false, cancellationToken),
            "achievements" => GetAchievementsAsync(AdminOptionLimit, activeOnly: false, cancellationToken),
            "news" => GetNewsAsync(AdminOptionLimit, cancellationToken),
            _ => Task.FromResult<IReadOnlyList<QuestStepActionOption>>([])
        };

    /// <inheritdoc />
    public async Task<QuestStepActionCandidateSets> GetCandidateSetsAsync(
        CancellationToken cancellationToken = default)
    {
        var quests = await GetQuestsAsync(AiCandidateLimit, activeOnly: true, cancellationToken);
        var surveys = await GetSurveysAsync(AiCandidateLimit, activeOnly: true, cancellationToken);
        var events = await GetEventsAsync(AiCandidateLimit, activeOnly: true, cancellationToken);
        var achievements = await GetAchievementsAsync(
            AiCandidateLimit,
            activeOnly: true,
            cancellationToken);

        return new QuestStepActionCandidateSets(quests, surveys, events, achievements);
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetQuestsAsync(
        int limit,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = dbContext.Quests.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(quest =>
                quest.IsActive
                && (quest.StartAt == null || quest.StartAt <= now)
                && (quest.EndAt == null || quest.EndAt >= now));
        }

        var rows = await query
            .OrderByDescending(quest => quest.CreatedAt)
            .ThenByDescending(quest => quest.Id)
            .Take(limit)
            .Select(quest => new
            {
                quest.Id,
                quest.Title,
                quest.EndAt,
                quest.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Id.ToString(),
                row.Title,
                row.EndAt is DateTime endAt ? $"до {endAt:yyyy-MM-dd}" : null,
                row.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetSurveysAsync(
        int limit,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = dbContext.Surveys.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(survey =>
                survey.IsActive
                && (survey.StartAt == null || survey.StartAt <= now)
                && (survey.EndAt == null || survey.EndAt >= now));
        }

        var rows = await query
            .OrderByDescending(survey => survey.CreatedAt)
            .ThenByDescending(survey => survey.Id)
            .Take(limit)
            .Select(survey => new
            {
                survey.Id,
                survey.Title,
                survey.EndAt,
                survey.IsActive,
                survey.AllowMultipleResponses
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Id.ToString(),
                row.Title,
                BuildSurveySubtitle(row.EndAt, row.AllowMultipleResponses),
                row.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetEventsAsync(
        int limit,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = dbContext.Events.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(eventItem => eventItem.IsActive && eventItem.EndAt >= now);
        }

        var rows = await query
            .OrderBy(eventItem => eventItem.StartAt)
            .ThenBy(eventItem => eventItem.Id)
            .Take(limit)
            .Select(eventItem => new
            {
                eventItem.Id,
                eventItem.Title,
                eventItem.StartAt,
                eventItem.EndAt,
                eventItem.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Id.ToString(),
                row.Title,
                $"{row.StartAt:yyyy-MM-dd} — {row.EndAt:yyyy-MM-dd}",
                row.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetProductsAsync(
        int limit,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(product => product.IsActive);
        }

        var rows = await query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Take(limit)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Price,
                product.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Id.ToString(),
                row.Name,
                $"{row.Price} баллов",
                row.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetAchievementsAsync(
        int limit,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Achievements.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(achievement => achievement.IsActive);
        }

        var rows = await query
            .OrderBy(achievement => achievement.Name)
            .ThenBy(achievement => achievement.Id)
            .Take(limit)
            .Select(achievement => new
            {
                achievement.Code,
                achievement.Name,
                achievement.IsActive
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Code,
                row.Name,
                row.Code,
                row.IsActive))
            .ToList();
    }

    private async Task<IReadOnlyList<QuestStepActionOption>> GetNewsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CompanyNews
            .AsNoTracking()
            .OrderByDescending(news => news.CreatedAt)
            .ThenByDescending(news => news.Id)
            .Take(limit)
            .Select(news => new
            {
                news.Id,
                news.Title,
                news.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuestStepActionOption(
                row.Id.ToString(),
                row.Title,
                row.CreatedAt.ToString("yyyy-MM-dd"),
                true))
            .ToList();
    }

    private static string? BuildSurveySubtitle(DateTime? endAt, bool allowMultipleResponses)
    {
        var parts = new List<string>(2);

        if (endAt is DateTime end)
        {
            parts.Add($"до {end:yyyy-MM-dd}");
        }

        if (!allowMultipleResponses)
        {
            parts.Add("один ответ");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
