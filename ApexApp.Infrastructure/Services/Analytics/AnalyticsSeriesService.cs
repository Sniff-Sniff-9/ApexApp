using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Показатели, для которых можно построить временной ряд.
/// </summary>
public enum AnalyticsSeriesMetric
{
    /// <summary>Уникальные участники (объединение всех источников активности).</summary>
    Participants = 0,

    /// <summary>Действия (объединение всех источников активности).</summary>
    Actions = 1,

    /// <summary>Посещения мероприятий.</summary>
    EventsAttended = 2,

    /// <summary>Завершённые задания.</summary>
    QuestsCompleted = 3,

    /// <summary>Ответы на опросы.</summary>
    SurveysSubmitted = 4,

    /// <summary>Отправленные благодарности.</summary>
    ThanksSent = 5,

    /// <summary>Начисленные баллы.</summary>
    PointsEarned = 6,

    /// <summary>Потраченные баллы (сумма заказов).</summary>
    PointsSpent = 7,

    /// <summary>Количество заказов.</summary>
    Orders = 8
}

/// <summary>
/// Построение временных рядов по активности. Группировка выполняется в PostgreSQL
/// функцией <c>date_trunc</c> с учётом настроенного часового пояса, поэтому в память
/// приложения попадает только по одной строке на корзину.
/// </summary>
public interface IAnalyticsSeriesService
{
    /// <summary>
    /// Возвращает временной ряд показателя.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="metric">Показатель.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ряд или ошибка 400.</returns>
    Task<ServiceResult<AnalyticsSeries>> GetAsync(
        AnalyticsQuery query,
        AnalyticsSeriesMetric metric,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsSeriesService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsSeriesService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<AnalyticsSeries>> GetAsync(
        AnalyticsQuery query,
        AnalyticsSeriesMetric metric,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<AnalyticsSeries>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;
        var filter = AnalyticsCacheTags.Filter(query.DepartmentId);

        var series = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                $"series-{metric}",
                range.CacheTag,
                filter,
                AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.SeriesTtl,
            token => BuildAsync(range, query.DepartmentId, metric, token),
            cancellationToken);

        return ServiceResult<AnalyticsSeries>.Ok(series);
    }

    private async Task<AnalyticsSeries> BuildAsync(
        AnalyticsRange range,
        int? departmentId,
        AnalyticsSeriesMetric metric,
        CancellationToken cancellationToken)
    {
        var buckets = AnalyticsBuckets.Build(range, _options.MaxBuckets);
        var rows = await LoadRowsAsync(range, departmentId, metric, cancellationToken);
        var population = await populationService.GetAsync(cancellationToken);
        var headcount = departmentId is int department
            ? population.ActiveByDepartment.GetValueOrDefault(department)
            : population.ActiveUsers;

        var points = new List<AnalyticsSeriesPoint>(buckets.Count);
        long? previousValue = null;

        foreach (var bucket in buckets)
        {
            var match = rows.FirstOrDefault(row => row.Bucket == bucket.Start);
            var value = match?.Value ?? 0;
            var users = match?.Users ?? 0;

            points.Add(new AnalyticsSeriesPoint(
                bucket.Start,
                bucket.End,
                value,
                users,
                headcount >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(value, headcount), 3)
                    : null,
                AnalyticsMetrics.Round(AnalyticsMetrics.ChangePercent(value, previousValue))));

            previousValue = value;
        }

        var (title, unit) = Describe(metric);

        return new AnalyticsSeries(metric.ToString(), title, unit, points);
    }

    private async Task<IReadOnlyList<SeriesBucketRow>> LoadRowsAsync(
        AnalyticsRange range,
        int? departmentId,
        AnalyticsSeriesMetric metric,
        CancellationToken cancellationToken)
    {
        var part = range.GranularityCode;

        return metric switch
        {
            AnalyticsSeriesMetric.Participants or AnalyticsSeriesMetric.Actions => await LoadCombinedAsync(
                range,
                departmentId,
                part,
                cancellationToken),

            AnalyticsSeriesMetric.EventsAttended => await LoadEventsAsync(range, departmentId, part, cancellationToken),

            AnalyticsSeriesMetric.QuestsCompleted => await LoadQuestsAsync(range, departmentId, part, cancellationToken),

            AnalyticsSeriesMetric.SurveysSubmitted => await LoadSurveysAsync(range, departmentId, part, cancellationToken),

            AnalyticsSeriesMetric.ThanksSent => await LoadThanksAsync(range, departmentId, part, cancellationToken),

            AnalyticsSeriesMetric.PointsEarned => await LoadPointsAsync(range, departmentId, part, cancellationToken),

            AnalyticsSeriesMetric.PointsSpent => await LoadSpentAsync(range, departmentId, part, cancellationToken),

            _ => await LoadOrdersAsync(range, departmentId, part, cancellationToken)
        };
    }

    private Task<List<SeriesBucketRow>> LoadCombinedAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, a.at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT a.user_id)::bigint AS "Users"
            FROM (
                SELECT ep.user_id, ep.attended_at AS at
                FROM public.event_participants ep
                WHERE ep.status = 'attended' AND ep.attended_at >= {range.From} AND ep.attended_at < {range.To}
                UNION ALL
                SELECT qp.user_id, qp.completed_at
                FROM public.quest_participations qp
                WHERE qp.status = 'completed' AND qp.completed_at >= {range.From} AND qp.completed_at < {range.To}
                UNION ALL
                SELECT sr.user_id, sr.submitted_at
                FROM public.survey_responses sr
                WHERE sr.submitted_at >= {range.From} AND sr.submitted_at < {range.To}
                UNION ALL
                SELECT t.from_user_id, t.created_at
                FROM public.thanks t
                WHERE t.created_at >= {range.From} AND t.created_at < {range.To}
            ) a
            WHERE (CAST({departmentId} AS integer) IS NULL
                   OR a.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadEventsAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, ep.attended_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT ep.user_id)::bigint AS "Users"
            FROM public.event_participants ep
            WHERE ep.status = 'attended'
              AND ep.attended_at >= {range.From} AND ep.attended_at < {range.To}
              AND (CAST({departmentId} AS integer) IS NULL
                   OR ep.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadQuestsAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, qp.completed_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT qp.user_id)::bigint AS "Users"
            FROM public.quest_participations qp
            WHERE qp.status = 'completed'
              AND qp.completed_at >= {range.From} AND qp.completed_at < {range.To}
              AND (CAST({departmentId} AS integer) IS NULL
                   OR qp.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadSurveysAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, sr.submitted_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT sr.user_id)::bigint AS "Users"
            FROM public.survey_responses sr
            WHERE sr.submitted_at >= {range.From} AND sr.submitted_at < {range.To}
              AND (CAST({departmentId} AS integer) IS NULL
                   OR sr.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadThanksAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, t.created_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT t.from_user_id)::bigint AS "Users"
            FROM public.thanks t
            WHERE t.created_at >= {range.From} AND t.created_at < {range.To}
              AND (CAST({departmentId} AS integer) IS NULL
                   OR t.from_user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadPointsAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, t.created_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COALESCE(SUM(t.amount), 0)::bigint AS "Value",
                   COUNT(DISTINCT t.user_id)::bigint AS "Users"
            FROM public.transactions t
            WHERE t.created_at >= {range.From} AND t.created_at < {range.To}
              AND t.type IN ('activity_reward'::transaction_type, 'thanks'::transaction_type,
                             'achievement_reward'::transaction_type, 'manager_bonus'::transaction_type,
                             'daily_bonus'::transaction_type)
              AND (CAST({departmentId} AS integer) IS NULL
                   OR t.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadSpentAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, o.created_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COALESCE(SUM(o.total_amount), 0)::bigint AS "Value",
                   COUNT(DISTINCT o.user_id)::bigint AS "Users"
            FROM public.orders o
            WHERE o.created_at >= {range.From} AND o.created_at < {range.To}
              AND o.status NOT IN ('cancelled'::order_status, 'refunded'::order_status)
              AND (CAST({departmentId} AS integer) IS NULL
                   OR o.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private Task<List<SeriesBucketRow>> LoadOrdersAsync(
        AnalyticsRange range,
        int? departmentId,
        string part,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SeriesBucketRow>($"""
            SELECT (date_trunc({part}, o.created_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value",
                   COUNT(DISTINCT o.user_id)::bigint AS "Users"
            FROM public.orders o
            WHERE o.created_at >= {range.From} AND o.created_at < {range.To}
              AND o.status NOT IN ('cancelled'::order_status, 'refunded'::order_status)
              AND (CAST({departmentId} AS integer) IS NULL
                   OR o.user_id IN (SELECT u.id FROM public.users u WHERE u.department_id = CAST({departmentId} AS integer)))
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

    private static (string Title, string Unit) Describe(AnalyticsSeriesMetric metric) => metric switch
    {
        AnalyticsSeriesMetric.Participants => ("Уникальные участники", "count"),
        AnalyticsSeriesMetric.Actions => ("Действия", "count"),
        AnalyticsSeriesMetric.EventsAttended => ("Посещения мероприятий", "count"),
        AnalyticsSeriesMetric.QuestsCompleted => ("Завершённые задания", "count"),
        AnalyticsSeriesMetric.SurveysSubmitted => ("Ответы на опросы", "count"),
        AnalyticsSeriesMetric.ThanksSent => ("Отправленные благодарности", "count"),
        AnalyticsSeriesMetric.PointsEarned => ("Начислено баллов", "points"),
        AnalyticsSeriesMetric.PointsSpent => ("Потрачено баллов", "points"),
        _ => ("Заказы", "count")
    };
}
