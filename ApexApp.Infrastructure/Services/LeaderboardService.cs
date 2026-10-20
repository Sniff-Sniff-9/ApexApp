using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class LeaderboardService(
    AppDbContext dbContext) : ILeaderboardService
{
    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetAsync(
        int days,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.MiddleName,
                u.AvatarUrl,

                ThanksCount = dbContext.Set<Thank>()
                    .LongCount(t =>
                        t.ToUserId == u.Id &&
                        t.CreatedAt >= startDate)
            })
            .OrderByDescending(u => u.ThanksCount)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return users
            .Select((u, index) => new LeaderboardEntryDto(
                Rank: index + 1,
                UserId: u.Id,
                Name: string.IsNullOrWhiteSpace(u.MiddleName)
                    ? $"{u.FirstName} {u.LastName}"
                    : $"{u.FirstName} {u.MiddleName} {u.LastName}",
                Avatar: u.AvatarUrl,
                ThanksCount: u.ThanksCount))
            .ToList();
    }
}