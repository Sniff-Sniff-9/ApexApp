using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Лидерборд сотрудников по баллу вовлечённости.
/// </summary>
public sealed class LeaderboardService(
    AppDbContext dbContext,
    IAnalyticsEngagementLoader engagementLoader,
    ICacheService cache) : ILeaderboardService
{
    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Leaderboard(limit);

        if (cache.TryGet<IReadOnlyList<LeaderboardEntryDto>>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        // The ranking metric is the engagement score, so the window is the canonical
        // EngagementScoring.WindowDays window. A caller-supplied window would make the
        // leaderboard disagree with the per-user engagement endpoint.
        var snapshot = await engagementLoader.LoadAsync(DateTime.UtcNow, cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.MiddleName,
                u.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        // Active users without any activity in the window have no snapshot; they keep
        // zero points and the lowest tier, exactly like the per-user endpoint reports.
        var lowestTier = EngagementScoring.Tiers[0];

        IReadOnlyList<LeaderboardEntryDto> leaderboard = users
            .Select(u => new
            {
                User = u,
                Engagement = snapshot.ByUser.GetValueOrDefault(u.Id)
            })
            .OrderByDescending(u => u.Engagement?.Score ?? 0d)
            .ThenBy(u => u.User.LastName)
            .ThenBy(u => u.User.FirstName)
            .ThenBy(u => u.User.Id)
            .Take(limit)
            .Select((u, index) => new LeaderboardEntryDto(
                Rank: index + 1,
                UserId: u.User.Id,
                Name: string.IsNullOrWhiteSpace(u.User.MiddleName)
                    ? $"{u.User.FirstName} {u.User.LastName}"
                    : $"{u.User.FirstName} {u.User.MiddleName} {u.User.LastName}",
                Avatar: u.User.AvatarUrl,
                Score: u.Engagement?.Score ?? 0d,
                Status: u.Engagement?.Status ?? lowestTier.Code,
                StatusTitle: u.Engagement?.StatusTitle ?? lowestTier.Title,
                StatusRank: u.Engagement?.StatusRank ?? lowestTier.Rank))
            .ToList();

        cache.Set(cacheKey, leaderboard, CacheKeys.LeaderboardTtl);

        return leaderboard;
    }
}
