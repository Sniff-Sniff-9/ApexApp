using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Detects engagement level increases by comparing the current calculated level
/// with the last <c>new_activity_level</c> notification. The notification history
/// is used as the level baseline, so no additional table or migration is required.
/// </summary>
public sealed class EngagementLevelNotifier(
    AppDbContext dbContext,
    IEngagementService engagementService,
    INotificationService notificationService,
    ICacheService cache,
    ILogger<EngagementLevelNotifier> logger) : IEngagementLevelNotifier
{
    private const int FirstLevelRank = 1;

    public async Task TryNotifyAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        try
        {
            cache.Remove(CacheKeys.Engagement(userId));

            var current = await engagementService.GetCurrentAsync(userId, cancellationToken);

            if (!current.IsSuccess || current.Value is null)
            {
                return;
            }

            var summary = current.Value;
            var baselineRank = await GetBaselineRankAsync(userId, cancellationToken);

            if (summary.StatusRank <= baselineRank)
            {
                return;
            }

            await notificationService.CreateAsync(
                userId,
                NotificationTypes.NewActivityLevel,
                "Новый уровень активности",
                $"Вы достигли уровня «{summary.StatusTitle}».",
                new
                {
                    status = summary.Status,
                    statusTitle = summary.StatusTitle,
                    rank = summary.StatusRank,
                    score = summary.Score
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            // A notification must never break the business action that raised it.
            logger.LogError(
                exception,
                "Failed to create an activity-level notification for user {UserId}",
                userId);
        }
    }

    private async Task<int> GetBaselineRankAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var latest = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.UserId == userId &&
                notification.Type == NotificationTypes.NewActivityLevel)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest?.Data is null)
        {
            return FirstLevelRank;
        }

        try
        {
            return latest.Data.RootElement.TryGetProperty("rank", out var rank) &&
                   rank.TryGetInt32(out var value)
                ? value
                : FirstLevelRank;
        }
        catch (ObjectDisposedException)
        {
            return FirstLevelRank;
        }
    }
}
