using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class DepartmentAnalyticsService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    IAnalyticsActivityRollupService rollupService,
    IAnalyticsEngagementLoader engagementLoader,
    IAnalyticsSeriesService seriesService,
    IAnalyticsOverviewService overviewService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IDepartmentAnalyticsService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<DepartmentAnalyticsResult>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<DepartmentAnalyticsResult>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var result = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                "departments",
                range.CacheTag,
                AnalyticsCacheTags.Filter(query.DepartmentId),
                AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.DepartmentTtl,
            token => BuildAsync(range, token),
            cancellationToken);

        if (query.DepartmentId is int departmentId)
        {
            var filtered = result.Rows.Where(row => row.DepartmentId == departmentId).ToList();

            result = result with { Rows = filtered };
        }

        return ServiceResult<DepartmentAnalyticsResult>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DepartmentDetailResult>> GetDetailAsync(
        AnalyticsQuery query,
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return ServiceResult<DepartmentDetailResult>.Fail(400, "A valid department ID is required.");
        }

        var listResult = await GetAsync(query with { DepartmentId = departmentId }, cancellationToken);

        if (!listResult.IsSuccess)
        {
            return ServiceResult<DepartmentDetailResult>.Fail(
                listResult.StatusCode,
                listResult.Error!);
        }

        var row = listResult.Value!.Rows.FirstOrDefault();

        if (row is null)
        {
            return ServiceResult<DepartmentDetailResult>.Fail(404, "Department analytics was not found.");
        }

        var seriesResult = await seriesService.GetAsync(
            new AnalyticsQuery(
                listResult.Value.Range.From,
                listResult.Value.Range.To,
                listResult.Value.Range.Granularity,
                departmentId,
                1),
            AnalyticsSeriesMetric.Participants,
            cancellationToken);

        if (!seriesResult.IsSuccess)
        {
            return ServiceResult<DepartmentDetailResult>.Fail(
                seriesResult.StatusCode,
                seriesResult.Error!);
        }

        var overview = await overviewService.GetAsync(
            new AnalyticsQuery(
                listResult.Value.Range.From,
                listResult.Value.Range.To,
                listResult.Value.Range.Granularity,
                departmentId,
                1),
            cancellationToken);

        var formats = overview.IsSuccess
            ? overview.Value!.Formats
            : Array.Empty<ActivityFormatRow>();

        return ServiceResult<DepartmentDetailResult>.Ok(new DepartmentDetailResult(
            row,
            seriesResult.Value!,
            formats,
            listResult.Value.Range,
            listResult.Value.Caveats));
    }

    private async Task<DepartmentAnalyticsResult> BuildAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var rollup = await rollupService.GetAsync(range, cancellationToken);
        var currentEngagement = await engagementLoader.LoadAsync(range.To, cancellationToken);
        var previousEngagement = await engagementLoader.LoadAsync(range.PreviousTo, cancellationToken);
        var crossDepartmentThanks = await LoadCrossDepartmentThanksAsync(range, cancellationToken);

        // Lookup поддерживает null-ключ: сотрудники без отдела образуют отдельную группу.
        var currentByDepartment = rollup.ToLookup(row => row.DepartmentId);

        var departmentIds = population.DepartmentNames.Keys
            .Where(id => population.ActiveByDepartment.GetValueOrDefault(id) > 0
                         || currentByDepartment[id].Any())
            .ToList();

        var rows = new List<DepartmentRow>(departmentIds.Count + 1);

        foreach (var departmentId in departmentIds.OrderBy(id => id))
        {
            rows.Add(BuildRow(
                departmentId,
                population.DepartmentNames.GetValueOrDefault(departmentId) ?? $"Отдел {departmentId}",
                population,
                currentByDepartment[departmentId].ToList(),
                currentEngagement,
                previousEngagement,
                crossDepartmentThanks));
        }

        if (population.ActiveWithoutDepartment > 0 || currentByDepartment[null].Any())
        {
            rows.Add(BuildRow(
                null,
                "Без отдела",
                population,
                currentByDepartment[null].ToList(),
                currentEngagement,
                previousEngagement,
                crossDepartmentThanks));
        }

        AssignRanks(rows);
        AssignRankChanges(rows, rollup);

        var caveats = new List<string>(range.Caveats)
        {
            "Активность относится к текущему отделу сотрудника: история переводов между отделами не хранится.",
            "Отделы с численностью меньше " + _options.MinCohort +
            " сотрудников не участвуют в рейтинге: на малых выборках доли недостоверны.",
            "Средний балл вовлечённости включает сотрудников без активности с нулевым баллом."
        };

        return new DepartmentAnalyticsResult(
            rows
                .OrderBy(row => row.Rank ?? int.MaxValue)
                .ThenByDescending(row => row.ActivityRate ?? -1d)
                .ThenBy(row => row.Department, StringComparer.Ordinal)
                .ToList(),
            range,
            caveats);
    }

    private DepartmentRow BuildRow(
        int? departmentId,
        string departmentName,
        AnalyticsPopulation population,
        IReadOnlyList<UserActivityRow> rows,
        EngagementSnapshot currentEngagement,
        EngagementSnapshot previousEngagement,
        IReadOnlyDictionary<int, DepartmentThanksStats> crossDepartmentThanks)
    {
        var headcount = departmentId is int id
            ? population.ActiveByDepartment.GetValueOrDefault(id)
            : population.ActiveWithoutDepartment;

        var active = rows.LongCount(row => row.Current.HasActivity);
        var actions = rows.Sum(row => row.Current.Actions);
        var events = rows.LongCount(row => row.Current.EventsAttended > 0);
        var quests = rows.LongCount(row => row.Current.QuestsJoined > 0 || row.Current.QuestsCompleted > 0);
        var questsJoined = rows.Sum(row => (long)row.Current.QuestsJoined);
        var questsCompleted = rows.Sum(row => (long)row.Current.QuestsCompleted);
        var surveys = rows.LongCount(row => row.Current.SurveysSubmitted > 0);
        var thanksSent = rows.Sum(row => (long)row.Current.ThanksSent);
        var thanksReceived = rows.Sum(row => (long)row.Current.ThanksReceived);
        var units = rows.Sum(row => row.Current.UnitsOrdered);
        var points = rows.Sum(row => row.Current.PointsSpent);

        var reliable = headcount >= _options.MinCohort;
        var departmentUsers = departmentId is int departmentIdValue
            ? population.ActiveUserIds
                .Where(userId => population.DepartmentIdByUser.GetValueOrDefault(userId) == departmentIdValue)
                .ToList()
            : population.ActiveUserIds
                .Where(userId => population.DepartmentIdByUser.GetValueOrDefault(userId) is null)
                .ToList();

        var currentScores = departmentUsers
            .Select(userId => currentEngagement.ByUser.GetValueOrDefault(userId)?.Score ?? 0d)
            .ToList();

        var previousScores = departmentUsers
            .Select(userId => previousEngagement.ByUser.GetValueOrDefault(userId)?.Score ?? 0d)
            .ToList();

        var currentAverage = currentScores.Count > 0 ? currentScores.Average() : (double?)null;
        var previousAverage = previousScores.Count > 0 ? previousScores.Average() : (double?)null;

        var thanksStats = crossDepartmentThanks.GetValueOrDefault(departmentId ?? UnassignedDepartmentKey);

        return new DepartmentRow(
            departmentId,
            departmentName,
            headcount,
            active,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(active, headcount), 3) : null,
            active > 0 ? AnalyticsMetrics.Round((double)actions / active, 1) : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Per100(events, headcount), 1) : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Per100(quests, headcount), 1) : null,
            questsJoined >= _options.MinCohort
                ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(questsCompleted, questsJoined), 3)
                : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Per100(surveys, headcount), 1) : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(thanksSent, headcount), 2) : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(thanksReceived, headcount), 2) : null,
            thanksStats is null || thanksStats.Total < _options.MinCohort
                ? null
                : AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(thanksStats.Cross, thanksStats.Total), 3),
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(units, headcount), 2) : null,
            reliable ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(points, headcount), 1) : null,
            currentAverage is null ? null : AnalyticsMetrics.Round(currentAverage),
            currentAverage is null || previousAverage is null
                ? null
                : AnalyticsMetrics.Round(currentAverage - previousAverage, 1),
            null,
            null,
            reliable);
    }

    private static void AssignRanks(List<DepartmentRow> rows)
    {
        var ranked = rows
            .Where(row => row.IsReliable && row.ActivityRate is not null)
            .OrderByDescending(row => row.ActivityRate)
            .ThenByDescending(row => row.ActiveEmployees)
            .ToList();

        for (var index = 0; index < ranked.Count; index++)
        {
            var rank = index + 1;
            var position = rows.IndexOf(ranked[index]);
            rows[position] = rows[position] with { Rank = rank };
        }
    }

    /// <summary>
    /// Считает изменение места: сравнивает текущий рейтинг с рейтингом по предыдущему периоду.
    /// </summary>
    private static void AssignRankChanges(List<DepartmentRow> rows, IReadOnlyList<UserActivityRow> rollup)
    {
        // Активность предыдущего периода по отделам: сотрудники без отдела образуют отдельную группу.
        var previousActive = rollup
            .Where(row => row.Previous.HasActivity)
            .ToLookup(row => row.DepartmentId);

        var previousRanks = rows
            .Where(row => row.IsReliable && row.Headcount > 0)
            .Select(row => new
            {
                Key = row.DepartmentId ?? UnassignedDepartmentKey,
                Rate = (double)previousActive[row.DepartmentId].Count() / row.Headcount
            })
            .OrderByDescending(row => row.Rate)
            .ThenBy(row => row.Key)
            .Select((row, index) => new { row.Key, Rank = index + 1 })
            .ToDictionary(row => row.Key, row => row.Rank);

        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Rank is not int currentRank)
            {
                continue;
            }

            var previousRank = previousRanks.GetValueOrDefault(
                rows[index].DepartmentId ?? UnassignedDepartmentKey);

            if (previousRank > 0)
            {
                // Положительное значение означает, что отдел поднялся в рейтинге.
                rows[index] = rows[index] with { RankChange = previousRank - currentRank };
            }
        }
    }

    private async Task<IReadOnlyDictionary<int, DepartmentThanksStats>> LoadCrossDepartmentThanksAsync(
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
            .GroupBy(row => new
            {
                row.FromDepartmentId,
                IsCross = row.ToDepartmentId != row.FromDepartmentId
            })
            .Select(group => new
            {
                group.Key.FromDepartmentId,
                group.Key.IsCross,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.FromDepartmentId ?? UnassignedDepartmentKey)
            .ToDictionary(
                group => group.Key,
                group => new DepartmentThanksStats(
                    group.Sum(row => row.Count),
                    group.Where(row => row.IsCross).Sum(row => row.Count)));
    }

    /// <summary>Ключ для сотрудников без отдела: идентификаторы отделов начинаются с единицы.</summary>
    private const int UnassignedDepartmentKey = 0;

    private sealed record DepartmentThanksStats(int Total, int Cross);
}
