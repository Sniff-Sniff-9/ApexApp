namespace ApexApp.Api.DTOs;

/// <summary>Badge shown on a catalog product or variant.</summary>
public sealed record CatalogBadgeDto(
    int Id,
    string Code,
    string Name,
    string Color,
    int SortOrder,
    int? ProductVariantId);

/// <summary>Catalog product with effective pricing and banners.</summary>
public sealed record CatalogProductDto(
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
    IReadOnlyList<CatalogBadgeDto> Badges);

/// <summary>Catalog variant with effective pricing and banners.</summary>
public sealed record CatalogVariantDto(
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
    IReadOnlyList<CatalogBadgeDto> Badges);
