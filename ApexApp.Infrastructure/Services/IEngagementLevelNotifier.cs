namespace ApexApp.Infrastructure.Services;

/// <summary>Creates a notification when a user's engagement level increases.</summary>
public interface IEngagementLevelNotifier
{
    Task TryNotifyAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
