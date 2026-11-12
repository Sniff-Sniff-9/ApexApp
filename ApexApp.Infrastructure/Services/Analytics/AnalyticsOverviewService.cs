using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Строит обзор платформы из общих агрегатов активности, справочника сотрудников
/// и агрегатов форматов. Показатели не пересчитываются в каждом отчёте.
/// </summary>
public sealed class AnalyticsOverviewService(
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    IAnalyticsActivityRollupService rollupService,
    IAnalyticsActivityAggregates activityAggregates,
    AppDbContext dbContext,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsOverviewService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<PlatformOverview>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<PlatformOverview>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;
        var filter = AnalyticsCacheTags.Filter(query.DepartmentId);

        var overview = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("overview", range.CacheTag, filter, AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.OverviewTtl,
            token => BuildAsync(range, query.DepartmentId, token),
            cancellationToken);

        return ServiceResult<PlatformOverview>.Ok(overview);
    }

    private async Task<PlatformOverview> BuildAsync(
        AnalyticsRange range,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var rows = await rollupService.GetAsync(range, cancellationToken);
        var scopedRows = departmentId is int scope
            ? rows.Where(row => row.DepartmentId == scope).ToList()
            : rows;

        var headcount = departmentId is int scopedDepartment
            ? population.ActiveByDepartment.GetValueOrDefault(scopedDepartment)
            : population.ActiveUsers;

        var current = scopedRows.Select(row => row.Current).ToList();
        var previous = scopedRows.Select(row => row.Previous).ToList();

        var participants = current.LongCount(segment => segment.HasActivity);
        var previousParticipants = previous.LongCount(segment => segment.HasActivity);
        var actions = current.Sum(segment => segment.Actions);
        var previousActions = previous.Sum(segment => segment.Actions);

        var priorActive = await populationService.GetPreviouslyActiveUsersAsync(
            range.PreviousFrom,
            cancellationToken);

        var newParticipants = scopedRows.LongCount(row =>
            row.Current.HasActivity && !priorActive.Contains(row.UserId));

        var returning = scopedRows.LongCount(row => row.Current.HasActivity && row.Previous.HasActivity);
        var churned = scopedRows.LongCount(row => !row.Current.HasActivity && row.Previous.HasActivity);

        var pointsEarned = current.Sum(segment => segment.PointsEarned);
        var previousPointsEarned = previous.Sum(segment => segment.PointsEarned);
        var pointsSpent = current.Sum(segment => segment.PointsSpent);
        var previousPointsSpent = previous.Sum(segment => segment.PointsSpent);
        var orders = current.Sum(segment => segment.Orders);
        var previousOrders = previous.Sum(segment => segment.Orders);
        var unitsOrdered = current.Sum(segment => segment.UnitsOrdered);

        var eventsAttended = current.Sum(segment => segment.EventsAttended);
        var previousEventsAttended = previous.Sum(segment => segment.EventsAttended);
        var questsCompleted = current.Sum(segment => segment.QuestsCompleted);
        var previousQuestsCompleted = previous.Sum(segment => segment.QuestsCompleted);
        var questsJoined = current.Sum(segment => segment.QuestsJoined);
        var surveysSubmitted = current.Sum(segment => segment.SurveysSubmitted);
        var previousSurveysSubmitted = previous.Sum(segment => segment.SurveysSubmitted);
        var thanksSent = current.Sum(segment => segment.ThanksSent);
        var previousThanksSent = previous.Sum(segment => segment.ThanksSent);
        var thanksReceived = current.Sum(segment => segment.ThanksReceived);

        var newUsers = await populationService.CountNewUsersAsync(
            range.From,
            range.To,
            departmentId,
            cancellationToken);

        var balances = await LoadBalancesAsync(cancellationToken);
        var pointsBySource = await LoadPointsBySourceAsync(range, cancellationToken);

        var formats = await BuildFormatsAsync(
            range,
            scopedRows,
            headcount,
            pointsBySource,
            cancellationToken);

        // В разрезе отдела численность берётся по отделу, а показатели,
        // которые нельзя корректно ограничить отделом, не показываются.
        var headcountMetrics = departmentId is null
            ? new List<MetricValue>
            {
                MetricValue.Count("total_users", "Всего учётных записей", population.TotalUsers),
                MetricValue.Count("active_users", "Активные сотрудники", population.ActiveUsers),
                MetricValue.Count("inactive_users", "Неактивные учётные записи", population.InactiveUsers),
                MetricValue.Count("new_users", "Новые сотрудники", newUsers),
                MetricValue.Count("unassigned_users", "Активные без отдела", population.ActiveWithoutDepartment)
            }
            : new List<MetricValue>
            {
                MetricValue.Count("active_users", "Активные сотрудники отдела", headcount),
                MetricValue.Count("new_users", "Новые сотрудники отдела", newUsers)
            };

        var participationMetrics = new List<MetricValue>
        {
            MetricValue.Count("participants", "Участники", participants),
            MetricValue.Rate("participation_rate", "Доля участия", participants, headcount, _options.MinCohort),
            MetricValue.Count("new_participants", "Новые участники", newParticipants),
            MetricValue.Count("returning_participants", "Вернувшиеся участники", returning),
            MetricValue.Count("churned_participants", "Потерянные участники", churned)
        };

        var volumeMetrics = new List<MetricValue>
        {
            MetricValue.Count("actions", "Действия", actions),
            MetricValue.Count(
                "actions_per_participant",
                "Действий на участника",
                participants > 0 ? (long)Math.Round((double)actions / participants, MidpointRounding.AwayFromZero) : 0),
            MetricValue.Count("events_attended", "Посещения мероприятий", eventsAttended),
            MetricValue.Count("quests_joined", "Начатые задания", questsJoined),
            MetricValue.Count("quests_completed", "Завершённые задания", questsCompleted),
            MetricValue.Count("surveys_submitted", "Ответы на опросы", surveysSubmitted),
            MetricValue.Count("thanks_sent", "Отправленные благодарности", thanksSent),
            MetricValue.Count("thanks_received", "Полученные благодарности", thanksReceived)
        };

        var economyMetrics = new List<MetricValue>
        {
            MetricValue.Count("points_earned", "Начислено баллов", pointsEarned, "points"),
            MetricValue.Count("points_spent", "Потрачено баллов", pointsSpent, "points"),
            MetricValue.Count("points_outstanding_personal", "Баллы на личных балансах", balances.Personal, "points"),
            MetricValue.Count("points_outstanding_reward", "Баллы на балансах руководителей", balances.Reward, "points"),
            MetricValue.Count("orders", "Заказы", orders),
            MetricValue.Count("units_ordered", "Единицы товара", unitsOrdered)
        };

        var deltas = new List<MetricDelta>
        {
            Delta("participants", "Участники", participants, previousParticipants),
            Delta("actions", "Действия", actions, previousActions),
            Delta("points_earned", "Начислено баллов", pointsEarned, previousPointsEarned),
            Delta("points_spent", "Потрачено баллов", pointsSpent, previousPointsSpent),
            Delta("orders", "Заказы", orders, previousOrders),
            Delta("events_attended", "Посещения мероприятий", eventsAttended, previousEventsAttended),
            Delta("quests_completed", "Завершённые задания", questsCompleted, previousQuestsCompleted),
            Delta("surveys_submitted", "Ответы на опросы", surveysSubmitted, previousSurveysSubmitted),
            Delta("thanks_sent", "Отправленные благодарности", thanksSent, previousThanksSent)
        };

        var caveats = new List<string>(range.Caveats);

        if (departmentId is not null)
        {
            caveats.Add(
                "Показатели ограничены одним отделом; остатки баллов на балансах показаны по всей платформе, " +
                "потому что баланс не привязан к отделу.");
        }

        if (pointsSpent == 0 && previousPointsSpent > 0)
        {
            caveats.Add("В текущем окне не было заказов: показатель потраченных баллов равен нулю.");
        }

        return new PlatformOverview(
            range,
            headcountMetrics,
            participationMetrics,
            volumeMetrics,
            economyMetrics,
            deltas,
            formats,
            caveats);
    }

    private async Task<IReadOnlyList<ActivityFormatRow>> BuildFormatsAsync(
        AnalyticsRange range,
        IReadOnlyList<UserActivityRow> rows,
        long headcount,
        IReadOnlyDictionary<string, long> pointsBySource,
        CancellationToken cancellationToken)
    {
        var eventAggregate = await activityAggregates.GetEventsAsync(range, cancellationToken);
        var questAggregate = await activityAggregates.GetQuestsAsync(range, cancellationToken);
        var surveyAggregate = await activityAggregates.GetSurveysAsync(range, cancellationToken);

        var eventParticipants = rows.LongCount(row => row.Current.EventsAttended > 0);
        var eventActions = rows.Sum(row => (long)row.Current.EventsAttended);
        var eventRepeat = rows.LongCount(row => row.Current.EventsAttended >= 2);

        var questParticipants = rows.LongCount(row => row.Current.QuestsJoined > 0 || row.Current.QuestsCompleted > 0);
        var questActions = rows.Sum(row => (long)row.Current.QuestsCompleted);
        var questRepeat = rows.LongCount(row => row.Current.QuestsCompleted >= 2);

        var surveyParticipants = rows.LongCount(row => row.Current.SurveysSubmitted > 0);
        var surveyActions = rows.Sum(row => (long)row.Current.SurveysSubmitted);
        var surveyRepeat = rows.LongCount(row => row.Current.SurveysSubmitted >= 2);

        var eventDenominator = eventAggregate.Attended + eventAggregate.NoShow;
        var questDenominator = questAggregate.Completed + questAggregate.Cancelled + questAggregate.InProgress;

        var eventReliable = headcount >= _options.MinCohort && eventDenominator >= _options.MinCohort;
        var questReliable = headcount >= _options.MinCohort && questDenominator >= _options.MinCohort;
        var surveyReliable = headcount >= _options.MinCohort;

        return
        [
            new ActivityFormatRow(
                "event",
                "Мероприятия",
                eventAggregate.Activities,
                eventParticipants,
                eventActions,
                surveyReliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(eventParticipants, headcount), 3) : null,
                eventReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(eventAggregate.Attended, eventDenominator), 3)
                    : null,
                eventReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(eventRepeat, eventParticipants), 3)
                    : null,
                eventAggregate.Attended >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(eventAggregate.Claimed, eventAggregate.Attended), 3)
                    : null,
                pointsBySource.GetValueOrDefault("event"),
                eventReliable),

            new ActivityFormatRow(
                "quest",
                "Задания",
                questAggregate.Activities,
                questParticipants,
                questActions,
                surveyReliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(questParticipants, headcount), 3) : null,
                questReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(questAggregate.Completed, questDenominator), 3)
                    : null,
                questReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(questRepeat, questParticipants), 3)
                    : null,
                questAggregate.Completed >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(questAggregate.Claimed, questAggregate.Completed), 3)
                    : null,
                pointsBySource.GetValueOrDefault("quest"),
                questReliable),

            new ActivityFormatRow(
                "survey",
                "Опросы",
                surveyAggregate.Activities,
                surveyParticipants,
                surveyActions,
                surveyReliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(surveyParticipants, headcount), 3) : null,
                surveyReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(surveyAggregate.Respondents, headcount), 3)
                    : null,
                surveyReliable
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(surveyRepeat, surveyParticipants), 3)
                    : null,
                null,
                pointsBySource.GetValueOrDefault("survey"),
                surveyReliable)
        ];
    }

    private async Task<BalancesSnapshot> LoadBalancesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Balances
            .AsNoTracking()
            .GroupBy(balance => balance.Type)
            .Select(group => new { Type = group.Key, Amount = group.Sum(balance => (long?)balance.Amount) ?? 0 })
            .ToListAsync(cancellationToken);

        var personal = rows.FirstOrDefault(row => row.Type == BalanceType.Personal)?.Amount ?? 0;
        var reward = rows.FirstOrDefault(row => row.Type == BalanceType.Reward)?.Amount ?? 0;

        return new BalancesSnapshot(personal, reward);
    }

    private async Task<IReadOnlyDictionary<string, long>> LoadPointsBySourceAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.ActivityReward
                && transaction.CreatedAt >= range.From
                && transaction.CreatedAt < range.To
                && transaction.RelatedEntityType != null)
            .GroupBy(transaction => transaction.RelatedEntityType!)
            .Select(group => new { Source = group.Key, Points = group.Sum(transaction => (long?)transaction.Amount) ?? 0 })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Source, row => row.Points, StringComparer.OrdinalIgnoreCase);
    }

    private static MetricDelta Delta(string code, string title, double? current, double? previous)
    {
        var change = AnalyticsMetrics.ChangePercent(current, previous);

        return new MetricDelta(code, title, current, previous, AnalyticsMetrics.Round(change), AnalyticsMetrics.Direction(change));
    }

    private sealed record BalancesSnapshot(long Personal, long Reward);
}
