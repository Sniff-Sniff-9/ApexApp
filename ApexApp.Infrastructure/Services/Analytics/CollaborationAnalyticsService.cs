using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class CollaborationAnalyticsService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : ICollaborationAnalyticsService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<CollaborationThanksResult>> GetThanksAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<CollaborationThanksResult>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var result = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                "collaboration-thanks",
                range.CacheTag,
                AnalyticsCacheTags.Filter(null),
                AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.DepartmentTtl,
            token => BuildAsync(range, cancellationToken: token),
            cancellationToken);

        return ServiceResult<CollaborationThanksResult>.Ok(result);
    }

    private async Task<CollaborationThanksResult> BuildAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var pairs = await LoadPairsAsync(range, cancellationToken);
        var previousPairs = await LoadPairsAsync(
            new AnalyticsRange(
                range.PreviousFrom,
                range.PreviousTo,
                range.PreviousFrom.AddDays(-range.LengthDays),
                range.PreviousFrom,
                range.Granularity,
                range.AsOf,
                range.TimeZone,
                range.TimeZoneOffsetMinutes,
                range.Caveats),
            cancellationToken);

        var series = await LoadSeriesAsync(range, cancellationToken);

        var total = pairs.Sum(row => row.Count);
        var cross = pairs.Where(row => row.IsCross).Sum(row => row.Count);
        var previousTotal = previousPairs.Sum(row => row.Count);
        var previousCross = previousPairs.Where(row => row.IsCross).Sum(row => row.Count);

        var topPairs = pairs
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.FromDepartmentId)
            .ThenBy(row => row.ToDepartmentId)
            .Take(_options.MaxTop)
            .Select(row => new DepartmentInteractionRow(
                row.FromDepartmentId,
                DepartmentName(population, row.FromDepartmentId),
                row.ToDepartmentId,
                DepartmentName(population, row.ToDepartmentId),
                row.Count,
                row.IsCross))
            .ToList();

        var caveats = new List<string>(range.Caveats)
        {
            "Взаимодействие измеряется только отправленными благодарностями: " +
            "других сущностей совместной работы в системе нет.",
            "Повторная благодарность одному сотруднику ограничена семью днями, " +
            "поэтому частые пары отражают устойчивые связи, а не общее число контактов.",
            "Матрица агрегирована по отделам; имена сотрудников не раскрываются."
        };

        return new CollaborationThanksResult(
            total,
            cross,
            total >= _options.MinCohort
                ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(cross, total), 3)
                : null,
            previousTotal >= _options.MinCohort
                ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(previousCross, previousTotal), 3)
                : null,
            topPairs,
            series,
            range,
            caveats);
    }

    private async Task<IReadOnlyList<PairRow>> LoadPairsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank =>
                thank.FromUserId != thank.ToUserId
                && thank.CreatedAt >= range.From
                && thank.CreatedAt < range.To)
            .Join(
                dbContext.Users.AsNoTracking(),
                thank => thank.FromUserId,
                user => user.Id,
                (thank, user) => new { thank.ToUserId, FromDepartmentId = user.DepartmentId })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.ToUserId,
                user => user.Id,
                (row, user) => new { row.FromDepartmentId, ToDepartmentId = user.DepartmentId })
            .GroupBy(row => new { row.FromDepartmentId, row.ToDepartmentId })
            .Select(group => new
            {
                group.Key.FromDepartmentId,
                group.Key.ToDepartmentId,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new PairRow(
                row.FromDepartmentId,
                row.ToDepartmentId,
                row.Count,
                row.FromDepartmentId != row.ToDepartmentId))
            .ToList();
    }

    private async Task<IReadOnlyList<CrossDepartmentSeriesPoint>> LoadSeriesAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var buckets = AnalyticsBuckets.Build(range, _options.MaxBuckets);

        var rows = await dbContext.Database.SqlQuery<CollaborationSeriesRow>($"""
            SELECT (date_trunc({range.GranularityCode}, t.created_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                   COUNT(*)::bigint AS "Total",
                   COUNT(*) FILTER (WHERE uf.department_id IS DISTINCT FROM ut.department_id)::bigint AS "Cross"
            FROM public.thanks t
            JOIN public.users uf ON uf.id = t.from_user_id
            JOIN public.users ut ON ut.id = t.to_user_id
            WHERE t.from_user_id <> t.to_user_id
              AND t.created_at >= {range.From}
              AND t.created_at < {range.To}
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(cancellationToken);

        return buckets
            .Select(bucket =>
            {
                var match = rows.FirstOrDefault(row => row.Bucket == bucket.Start);
                var total = match?.Total ?? 0;
                var cross = match?.Cross ?? 0;

                return new CrossDepartmentSeriesPoint(
                    bucket.Start,
                    bucket.End,
                    total,
                    cross,
                    total >= _options.MinCohort
                        ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(cross, total), 3)
                        : null);
            })
            .ToList();
    }

    private static string DepartmentName(AnalyticsPopulation population, int? departmentId) =>
        departmentId is int id
            ? population.DepartmentNames.GetValueOrDefault(id) ?? $"Отдел {id}"
            : "Без отдела";

    private sealed record PairRow(
        int? FromDepartmentId,
        int? ToDepartmentId,
        int Count,
        bool IsCross);

    /// <summary>Строка ряда межотдельских благодарностей.</summary>
    internal sealed class CollaborationSeriesRow
    {
        /// <summary>Начало корзины (UTC).</summary>
        public DateTime Bucket { get; set; }

        /// <summary>Всего благодарностей.</summary>
        public long Total { get; set; }

        /// <summary>Межотдельские благодарности.</summary>
        public long Cross { get; set; }
    }
}
