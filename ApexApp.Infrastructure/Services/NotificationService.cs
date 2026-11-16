using System.Text.Json;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Writes and reads per-user notifications. Notification creation is deliberately
/// lightweight so business services can call it after a successful commit.
/// </summary>
public sealed class NotificationService(
    AppDbContext dbContext) : INotificationService
{
    private const int MaxBatchSize = 500;

    public async Task CreateAsync(
        Guid userId,
        string type,
        string title,
        string message,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type.Trim(),
            Title = Truncate(title, 150),
            Message = message ?? string.Empty,
            Data = data is null ? null : JsonSerializer.SerializeToDocument(data),
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateManyAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string message,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        var recipients = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        if (recipients.Count == 0 || string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        foreach (var chunk in recipients.Chunk(MaxBatchSize))
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var userId in chunk)
            {
                dbContext.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = type.Trim(),
                    Title = Truncate(title, 150),
                    Message = message ?? string.Empty,
                    Data = data is null ? null : JsonSerializer.SerializeToDocument(data),
                    IsRead = false,
                    CreatedAt = now
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PagedResult<Notification>> GetAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, totalCount, page, pageSize);
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && !notification.IsRead,
                cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || notificationId <= 0)
        {
            return false;
        }

        var updated = await dbContext.Notifications
            .Where(notification =>
                notification.Id == notificationId &&
                notification.UserId == userId &&
                !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    notification => notification.IsRead,
                    true),
                cancellationToken);

        if (updated > 0)
        {
            return true;
        }

        return await dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                notification =>
                    notification.Id == notificationId &&
                    notification.UserId == userId,
                cancellationToken);
    }

    public Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications
            .Where(notification =>
                notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    notification => notification.IsRead,
                    true),
                cancellationToken);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}
