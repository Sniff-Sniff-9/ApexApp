using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>Administrator-facing badge definition and assignment operations.</summary>
public interface IProductBadgeService
{
    Task<List<ProductBadge>> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<List<ProductBadge>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductBadge>> CreateAsync(
        CreateProductBadgeCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductBadge>> UpdateAsync(
        int id,
        UpdateProductBadgeCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductBadgeSetting>> UpsertSettingsAsync(
        int badgeId,
        BadgeSettingsInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductBadgeAssignment>> AssignManualAsync(
        int productId,
        int badgeId,
        AssignProductBadgeCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveAssignmentAsync(
        int productId,
        long assignmentId,
        CancellationToken cancellationToken = default);

    Task<List<ProductBadgeAssignment>> GetAssignmentsAsync(
        int productId,
        CancellationToken cancellationToken = default);
}
