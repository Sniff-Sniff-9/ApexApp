namespace ApexApp.Infrastructure.Services;

/// <summary>Read model for the catalog with effective prices and active badges.</summary>
public interface ICatalogService
{
    Task<List<CatalogProduct>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<CatalogProduct?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<CatalogProduct>> GetAllAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<List<CatalogVariant>?> GetVariantsAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<CatalogVariant?> GetVariantAsync(
        int productId,
        int variantId,
        CancellationToken cancellationToken = default);
}
