using System.Security.Cryptography;
using System.Text;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class EngagementAnalyticsService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    IAnalyticsActivityRollupService rollupService,
    IAnalyticsEngagementLoader engagementLoader,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IEngagementAnalyticsService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<EngagementTrendResult>> GetTrendAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<EngagementTrendResult>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var trend = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                "engagement-trend",
                range.CacheTag,
                AnalyticsCacheTags.Filter(query.DepartmentId),
                AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.EngagementTtl,
            token => BuildTrendAsync(range, query.DepartmentId, token),
            cancellationToken);

        return ServiceResult<EngagementTrendResult>.Ok(trend);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DecliningEmployeesResult>> GetDecliningAsync(
        AnalyticsQuery query,
        bool includeNames,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<DecliningEmployeesResult>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var result = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                "engagement-declining",
                range.CacheTag,
                AnalyticsCacheTags.Filter(query.DepartmentId) + (includeNames ? "-names" : "-masked"),
                AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.EmployeeTtl,
            token => BuildDecliningAsync(range, query.DepartmentId, includeNames, token),
            cancellationToken);

        return ServiceResult<DecliningEmployeesResult>.Ok(result);
    }

    private async Task<EngagementTrendResult> BuildTrendAsync(
        AnalyticsRange range,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var buckets = AnalyticsBuckets.Build(range, _options.MaxBuckets);
        var periodCount = Math.Clamp(_options.EngagementTrendPeriods, 1, _options.MaxEngagementTrendPeriods);
        var periods = buckets.Count > periodCount
            ? buckets.Skip(buckets.Count - periodCount).ToList()
            : buckets;

        var employees = departmentId is int department
            ? population.ActiveUserIds
                .Where(userId => population.DepartmentIdByUser.GetValueOrDefault(userId) == department)
                .ToList()
            : population.ActiveUserIds.ToList();

        var headcount = employees.Count;
        var points = new List<EngagementTrendPoint>(periods.Count);
        double? previousAverage = null;

        foreach (var period in periods)
        {
            var snapshot = await engagementLoader.LoadAsync(period.End, cancellationToken);

            var scores = new List<double>(employees.Count);
            var tiers = new Dictionary<string, long>(StringComparer.Ordinal);
            var scored = 0L;
            var topTier = 0L;

            foreach (var userId in employees)
            {
                var summary = snapshot.ByUser.GetValueOrDefault(userId);
                var score = summary?.Score ?? 0d;

                scores.Add(score);

                if (summary is not null)
                {
                    scored++;
                    tiers[summary.Status] = tiers.GetValueOrDefault(summary.Status) + 1;

                    if (summary.StatusRank >= 4)
                    {
                        topTier++;
                    }
                }
            }

            var average = scores.Count > 0 ? scores.Average() : (double?)null;
            var median = AnalyticsMetrics.Median(scores);

            var tierCounts = EngagementScoring.Tiers
                .Select(tier => new EngagementTierCount(
                    tier.Code,
                    tier.Title,
                    tier.Rank,
                    tiers.GetValueOrDefault(tier.Code)))
                .ToList();

            points.Add(new EngagementTrendPoint(
                period.Start,
                period.End,
                snapshot.WindowStart,
                AnalyticsMetrics.Round(average),
                AnalyticsMetrics.Round(median),
                scored,
                headcount,
                topTier,
                AnalyticsMetrics.Round(previousAverage),
                average is null || previousAverage is null
                    ? null
                    : AnalyticsMetrics.Round(average - previousAverage, 1),
                tierCounts,
                headcount >= _options.MinCohort));

            previousAverage = average;
        }

        var caveats = new List<string>(range.Caveats)
        {
            "Вовлечённость рассчитывается действующей формулой платформы (окно 30 дней, взвешивание по свежести). " +
            "Для исторических периодов формула применяется к данным на конец периода.",
            "Сотрудники без активности учитываются с нулевым баллом: средний балл отражает вовлечённость всего штата, " +
            "а не только активных сотрудников."
        };

        return new EngagementTrendResult(
            points,
            EngagementScoring.WindowDays,
            range,
            caveats);
    }

    private async Task<DecliningEmployeesResult> BuildDecliningAsync(
        AnalyticsRange range,
        int? departmentId,
        bool includeNames,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var rollup = await rollupService.GetAsync(range, cancellationToken);

        var current = await engagementLoader.LoadAsync(range.To, cancellationToken);
        var previous = await engagementLoader.LoadAsync(range.PreviousTo, cancellationToken);

        var participants = rollup.LongCount(row => row.Current.HasActivity);
        var headcount = departmentId is int department
            ? population.ActiveByDepartment.GetValueOrDefault(department)
            : population.ActiveUsers;

        var candidates = new List<(Guid UserId, double Current, double Previous, long Actions)>();

        foreach (var row in rollup)
        {
            if (departmentId is int scope && row.DepartmentId != scope)
            {
                continue;
            }

            var currentScore = current.ByUser.GetValueOrDefault(row.UserId)?.Score ?? 0d;
            var previousScore = previous.ByUser.GetValueOrDefault(row.UserId)?.Score ?? 0d;
            var actions = row.Current.Actions + row.Previous.Actions;

            if (currentScore - previousScore <= -_options.DecliningScoreThreshold && actions >= 3)
            {
                candidates.Add((row.UserId, currentScore, previousScore, actions));
            }
        }

        var selected = candidates
            .OrderBy(candidate => candidate.Current - candidate.Previous)
            .ThenByDescending(candidate => candidate.Actions)
            .Take(_options.MaxDecliningEmployees)
            .ToList();

        var names = new Dictionary<Guid, string>();

        if (includeNames && selected.Count > 0)
        {
            var ids = selected.Select(candidate => candidate.UserId).ToList();

            var users = await dbContext.Users
                .AsNoTracking()
                .Where(user => ids.Contains(user.Id))
                .Select(user => new { user.Id, user.FirstName, user.LastName })
                .ToListAsync(cancellationToken);

            names = users.ToDictionary(
                user => user.Id,
                user => $"{user.FirstName} {user.LastName}".Trim());
        }

        var employees = selected
            .Select(candidate =>
            {
                var departmentIdForUser = population.DepartmentIdByUser.GetValueOrDefault(candidate.UserId);

                return new DecliningEmployee(
                    EmployeeReference(candidate.UserId),
                    includeNames ? names.GetValueOrDefault(candidate.UserId) : null,
                    departmentIdForUser,
                    departmentIdForUser is int id
                        ? population.DepartmentNames.GetValueOrDefault(id)
                        : null,
                    candidate.Current,
                    candidate.Previous,
                    AnalyticsMetrics.Round(candidate.Current - candidate.Previous, 1) ?? 0d,
                    candidate.Actions,
                    current.ByUser.GetValueOrDefault(candidate.UserId)?.LastActivityAt);
            })
            .ToList();

        var caveats = new List<string>(range.Caveats)
        {
            "Снижение вовлечённости — наблюдение, а не оценка сотрудника: на балл влияют доступность активностей и сезонность.",
            "Идентификатор сотрудника обезличен; имена доступны только ролям с правом просмотра персональной аналитики."
        };

        return new DecliningEmployeesResult(
            employees,
            candidates.Count,
            Math.Max(headcount - participants, 0),
            headcount,
            _options.DecliningScoreThreshold,
            range,
            caveats);
    }

    /// <summary>
    /// Возвращает устойчивый обезличенный идентификатор сотрудника.
    /// </summary>
    /// <param name="userId">Идентификатор сотрудника.</param>
    /// <returns>Строка из 16 байт в base64url.</returns>
    public static string EmployeeReference(Guid userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("N")));
        var truncated = hash[..16];

        return Convert.ToBase64String(truncated)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
