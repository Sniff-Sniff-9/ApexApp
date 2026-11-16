using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>Badge settings supplied by the administrator.</summary>
public sealed record BadgeSettingsInput(
    int? PeriodDays,
    long? Threshold,
    int? DurationDays,
    bool IsEnabled);

/// <summary>Data required to create a badge definition.</summary>
public sealed record CreateProductBadgeCommand(
    string Code,
    string Name,
    string Color,
    string AssignmentType,
    bool IsActive,
    int SortOrder,
    BadgeSettingsInput? Settings);

/// <summary>Data allowed when updating a badge definition.</summary>
public sealed record UpdateProductBadgeCommand(
    string Name,
    string Color,
    string AssignmentType,
    bool IsActive,
    int SortOrder);

/// <summary>Manual badge assignment request.</summary>
public sealed record AssignProductBadgeCommand(
    int? ProductVariantId,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>Discount data supplied by the administrator.</summary>
public sealed record ProductDiscountInput(
    int? ProductVariantId,
    int DiscountPercent,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive);

/// <summary>A badge attached to a catalog product or variant.</summary>
public sealed record CatalogBadge(
    int Id,
    string Code,
    string Name,
    string Color,
    int SortOrder,
    int? ProductVariantId);

/// <summary>A product with effective pricing and active badges.</summary>
public sealed record CatalogProduct(
    int Id,
    int? CategoryId,
    string Name,
    string? Description,
    long Price,
    long OriginalPrice,
    long FinalPrice,
    int? DiscountPercent,
    string? ImageUrl,
    int? Stock,
    bool IsActive,
    bool IsDigital,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CatalogBadge> Badges);

/// <summary>A product variant with effective pricing and active badges.</summary>
public sealed record CatalogVariant(
    int Id,
    int ProductId,
    string Name,
    int? CategoryId,
    string? ImageUrl,
    long Price,
    long OriginalPrice,
    long FinalPrice,
    int? DiscountPercent,
    int Stock,
    bool IsActive,
    IReadOnlyList<CatalogBadge> Badges);

/// <summary>Read model for company news with author information.</summary>
public sealed record CompanyNewsView(
    CompanyNews News,
    string AuthorName,
    string? AuthorAvatarUrl);
