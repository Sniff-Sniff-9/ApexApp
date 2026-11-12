using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Агрегированные показатели формата «мероприятия» за окно.
/// </summary>
/// <param name="Activities">Количество мероприятий, проводившихся в окне.</param>
/// <param name="Registered">Регистрации в статусе «зарегистрирован».</param>
/// <param name="Attended">Подтверждённое посещение.</param>
/// <param name="NoShow">Неявки.</param>
/// <param name="Cancelled">Отменённые регистрации.</param>
/// <param name="Claimed">Полученные награды.</param>
public sealed record EventFormatAggregate(
    long Activities,
    long Registered,
    long Attended,
    long NoShow,
    long Cancelled,
    long Claimed);

/// <summary>
/// Агрегированные показатели формата «задания» за окно.
/// </summary>
/// <param name="Activities">Количество заданий, существовавших в окне.</param>
/// <param name="InProgress">Участия в процессе.</param>
/// <param name="Completed">Завершённые участия.</param>
/// <param name="Cancelled">Отменённые участия.</param>
/// <param name="Claimed">Участия с обработанной наградой.</param>
public sealed record QuestFormatAggregate(
    long Activities,
    long InProgress,
    long Completed,
    long Cancelled,
    long Claimed);

/// <summary>
/// Агрегированные показатели формата «опросы» за окно.
/// </summary>
/// <param name="Activities">Количество опросов, существовавших в окне.</param>
/// <param name="Responses">Количество отправленных ответов.</param>
/// <param name="Respondents">Количество уникальных респондентов.</param>
public sealed record SurveyFormatAggregate(
    long Activities,
    long Responses,
    long Respondents);

/// <summary>
/// Общие агрегаты форматов активности. Используются обзором платформы и аналитикой
/// активностей, чтобы показатели рассчитывались ровно один раз.
/// </summary>
public interface IAnalyticsActivityAggregates
{
    /// <summary>Агрегаты формата «мероприятия».</summary>
    /// <param name="range">Окно расчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Агрегаты.</returns>
    Task<EventFormatAggregate> GetEventsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);

    /// <summary>Агрегаты формата «задания».</summary>
    /// <param name="range">Окно расчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Агрегаты.</returns>
    Task<QuestFormatAggregate> GetQuestsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);

    /// <summary>Агрегаты формата «опросы».</summary>
    /// <param name="range">Окно расчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Агрегаты.</returns>
    Task<SurveyFormatAggregate> GetSurveysAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsActivityAggregates(
    AppDbContext dbContext,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsActivityAggregates
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public Task<EventFormatAggregate> GetEventsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.Analytics("format-events", range.CacheTag, "-", "-"),
            _options.SeriesTtl,
            token => LoadEventsAsync(range, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<QuestFormatAggregate> GetQuestsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.Analytics("format-quests", range.CacheTag, "-", "-"),
            _options.SeriesTtl,
            token => LoadQuestsAsync(range, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<SurveyFormatAggregate> GetSurveysAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.Analytics("format-surveys", range.CacheTag, "-", "-"),
            _options.SeriesTtl,
            token => LoadSurveysAsync(range, token),
            cancellationToken);

    private async Task<EventFormatAggregate> LoadEventsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        // «Проводившиеся в окне» — мероприятие действительно, если оно ещё не закончилось
        // к началу окна и уже началось до его конца.
        var activities = await dbContext.Events
            .AsNoTracking()
            .LongCountAsync(
                eventItem => eventItem.EndAt >= range.From && eventItem.StartAt < range.To,
                cancellationToken);

        var statuses = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.Event.EndAt >= range.From && participant.Event.StartAt < range.To)
            .GroupBy(participant => participant.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var claimed = await dbContext.EventParticipants
            .AsNoTracking()
            .LongCountAsync(
                participant => participant.RewardClaimed
                               && participant.Event.EndAt >= range.From
                               && participant.Event.StartAt < range.To,
                cancellationToken);

        var registered = statuses
            .Where(row => row.Status == EventParticipantStatuses.Registered)
            .Sum(row => (long)row.Count);

        var attended = statuses
            .Where(row => row.Status == EventParticipantStatuses.Attended)
            .Sum(row => (long)row.Count);

        var noShow = statuses
            .Where(row => row.Status == EventParticipantStatuses.NoShow)
            .Sum(row => (long)row.Count);

        var cancelled = statuses
            .Where(row => row.Status == EventParticipantStatuses.Cancelled)
            .Sum(row => (long)row.Count);

        return new EventFormatAggregate(activities, registered, attended, noShow, cancelled, claimed);
    }

    private async Task<QuestFormatAggregate> LoadQuestsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var activities = await dbContext.Quests
            .AsNoTracking()
            .LongCountAsync(
                quest => quest.CreatedAt < range.To
                         && (quest.EndAt == null || quest.EndAt >= range.From),
                cancellationToken);

        var statuses = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.StartedAt >= range.From && participation.StartedAt < range.To)
            .GroupBy(participation => participation.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var claimed = await dbContext.QuestParticipations
            .AsNoTracking()
            .LongCountAsync(
                participation => participation.RewardClaimed
                                 && participation.CompletedAt >= range.From
                                 && participation.CompletedAt < range.To,
                cancellationToken);

        var inProgress = statuses
            .Where(row => row.Status == QuestParticipationStatuses.InProgress)
            .Sum(row => (long)row.Count);

        var completed = statuses
            .Where(row => row.Status == QuestParticipationStatuses.Completed)
            .Sum(row => (long)row.Count);

        var cancelled = statuses
            .Where(row => row.Status == QuestParticipationStatuses.Cancelled)
            .Sum(row => (long)row.Count);

        return new QuestFormatAggregate(activities, inProgress, completed, cancelled, claimed);
    }

    private async Task<SurveyFormatAggregate> LoadSurveysAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var activities = await dbContext.Surveys
            .AsNoTracking()
            .LongCountAsync(
                survey => survey.CreatedAt < range.To
                          && (survey.EndAt == null || survey.EndAt >= range.From),
                cancellationToken);

        var rangeResponses = dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SubmittedAt >= range.From && response.SubmittedAt < range.To);

        var responses = await rangeResponses.LongCountAsync(cancellationToken);

        var respondents = await rangeResponses
            .Select(response => response.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new SurveyFormatAggregate(activities, responses, respondents);
    }
}
