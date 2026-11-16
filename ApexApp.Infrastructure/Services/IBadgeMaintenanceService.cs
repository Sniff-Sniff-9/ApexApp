namespace ApexApp.Infrastructure.Services;

/// <summary>Materializes automatic badge assignments from catalog and sales data.</summary>
public interface IBadgeMaintenanceService
{
    Task RecalculateAllAsync(CancellationToken cancellationToken = default);

    Task RecalculateForProductAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task RecalculateBadgeAsync(
        string badgeCode,
        CancellationToken cancellationToken = default);
}
