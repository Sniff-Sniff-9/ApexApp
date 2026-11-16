using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>Per-user in-app notifications.</summary>
public interface INotificationService
{
    Task CreateAsync(
        Guid userId,
        string type,
        string title,
        string message,
        object? data = null,
        CancellationToken cancellationToken = default);

    Task CreateManyAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string message,
        object? data = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Notification>> GetAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
