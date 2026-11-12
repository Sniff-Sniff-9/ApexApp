using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Справочные данные аналитики: численность, отделы и признак наличия прошлой активности.
/// Все значения кешируются, потому что меняются редко и используются несколькими отчётами.
/// </summary>
public interface IAnalyticsPopulationService
{
    /// <summary>Возвращает численность и справочник отделов.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Справочные данные.</returns>
    Task<AnalyticsPopulation> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Считает количество учётных записей, созданных в окне.</summary>
    /// <param name="from">Начало окна.</param>
    /// <param name="to">Конец окна.</param>
    /// <param name="departmentId">Ограничение по отделу или <c>null</c> для всей платформы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество новых сотрудников.</returns>
    Task<long> CountNewUsersAsync(
        DateTime from,
        DateTime to,
        int? departmentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает сотрудников, у которых была активность до указанного момента.
    /// Используется для новых участников: они не должны учитываться повторно.
    /// </summary>
    /// <param name="before">Момент, до которого ищется активность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Множество идентификаторов сотрудников.</returns>
    Task<IReadOnlySet<Guid>> GetPreviouslyActiveUsersAsync(
        DateTime before,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsPopulationService(
    AppDbContext dbContext,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsPopulationService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public Task<AnalyticsPopulation> GetAsync(CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.AnalyticsReference("population"),
            _options.ReferenceTtl,
            LoadAsync,
            cancellationToken);

    /// <inheritdoc />
    public async Task<long> CountNewUsersAsync(
        DateTime from,
        DateTime to,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var box = await cache.GetOrCreateAsync(
            CacheKeys.Analytics(
                "new-users",
                $"{from:yyyyMMdd}-{to:yyyyMMdd}",
                AnalyticsCacheTags.Filter(departmentId),
                "-"),
            _options.ReferenceTtl,
            async token => new LongBox(await dbContext.Users
                .AsNoTracking()
                .LongCountAsync(
                    user => user.CreatedAt >= from
                            && user.CreatedAt < to
                            && (departmentId == null || user.DepartmentId == departmentId),
                    token)),
            cancellationToken);

        return box.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetPreviouslyActiveUsersAsync(
        DateTime before,
        CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync<HashSet<Guid>>(
            CacheKeys.Analytics("prior-active", $"{before:yyyyMMddHH}", "-", "-"),
            _options.ReferenceTtl,
            token => LoadPreviouslyActiveAsync(before, token),
            cancellationToken);
    }

    private async Task<HashSet<Guid>> LoadPreviouslyActiveAsync(
        DateTime before,
        CancellationToken cancellationToken)
    {
        // Four indexed projections instead of one UNION: each result is bounded by the
        // number of employees, so the set can be intersected in memory exactly.
        var eventUsers = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.Status == Common.EventParticipantStatuses.Attended
                && participant.AttendedAt < before)
            .Select(participant => participant.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var questUsers = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.Status == Common.QuestParticipationStatuses.Completed
                && participation.CompletedAt < before)
            .Select(participation => participation.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var surveyUsers = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SubmittedAt < before)
            .Select(response => response.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var thankUsers = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank => thank.CreatedAt < before)
            .Select(thank => thank.FromUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new HashSet<Guid>(eventUsers.Count + questUsers.Count);

        result.UnionWith(eventUsers);
        result.UnionWith(questUsers);
        result.UnionWith(surveyUsers);
        result.UnionWith(thankUsers);

        return result;
    }

    private async Task<AnalyticsPopulation> LoadAsync(CancellationToken cancellationToken)
    {
        var counts = await dbContext.Users
            .AsNoTracking()
            .GroupBy(user => new { user.DepartmentId, user.IsActive })
            .Select(group => new
            {
                group.Key.DepartmentId,
                group.Key.IsActive,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var totalUsers = counts.Sum(row => (long)row.Count);
        var activeUsers = counts.Where(row => row.IsActive).Sum(row => (long)row.Count);
        var inactiveUsers = totalUsers - activeUsers;
        var activeWithoutDepartment = counts
            .Where(row => row.IsActive && row.DepartmentId is null)
            .Sum(row => (long)row.Count);

        var activeByDepartment = counts
            .Where(row => row.IsActive && row.DepartmentId is not null)
            .GroupBy(row => row.DepartmentId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(row => (long)row.Count));

        var departmentByUser = await dbContext.Users
            .AsNoTracking()
            .Select(user => new { user.Id, user.DepartmentId })
            .ToDictionaryAsync(row => row.Id, row => row.DepartmentId, cancellationToken);

        var activeUserIds = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var departmentNames = await dbContext.Departments
            .AsNoTracking()
            .Select(department => new { department.Id, department.Name })
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);

        return new AnalyticsPopulation(
            totalUsers,
            activeUsers,
            inactiveUsers,
            activeWithoutDepartment,
            departmentByUser,
            departmentNames,
            activeByDepartment,
            activeUserIds.ToHashSet());
    }

    /// <summary>
    /// Кеш хранит ссылочные типы, поэтому числовой результат оборачивается в контейнер.
    /// </summary>
    private sealed record LongBox(long Value);
}
