using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Builds the catalog read model from products, active discounts and badge
/// assignments. Prices and badges are composed here so the API does not duplicate
/// the pricing rules.
/// </summary>
public sealed class CatalogService(
    AppDbContext dbContext,
    IProductDiscountService discountService,
    ICacheService cache) : ICatalogService
{
    public async Task<List<CatalogProduct>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.CatalogProducts(categoryId);

        if (cache.TryGet<List<CatalogProduct>>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && !product.IsDigital);

        if (categoryId is int id)
        {
            query = query.Where(product => product.CategoryId == id);
        }

        var products = await query
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        var result = await BuildProductsAsync(products, cancellationToken);
        cache.Set(cacheKey, result, CacheKeys.ProductsTtl);

        return result;
    }

    public async Task<CatalogProduct?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var cacheKey = CacheKeys.CatalogProduct(id);

        if (cache.TryGet<CatalogProduct>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id && item.IsActive && !item.IsDigital,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

        var result = (await BuildProductsAsync([product], cancellationToken))
            .FirstOrDefault();

        if (result is not null)
        {
            cache.Set(cacheKey, result, CacheKeys.ProductsTtl);
        }

        return result;
    }

    public async Task<List<CatalogProduct>> GetAllAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .AsQueryable();

        if (categoryId is int id)
        {
            query = query.Where(product => product.CategoryId == id);
        }

        var products = await query
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        return await BuildProductsAsync(products, cancellationToken);
    }

    public async Task<List<CatalogVariant>?> GetVariantsAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == productId)
            .OrderBy(variant => variant.Name)
            .ThenBy(variant => variant.Id)
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var discounts = await discountService.GetActiveForProductsAsync(
            [productId],
            now,
            cancellationToken);

        var badges = await LoadBadgesAsync([productId], now, cancellationToken);

        return variants
            .Select(variant =>
            {
                var (originalPrice, finalPrice, percent) = ProductPricing.Resolve(
                    product,
                    variant,
                    discounts,
                    now);

                return new CatalogVariant(
                    variant.Id,
                    variant.ProductId,
                    variant.Name,
                    variant.CategoryId,
                    variant.ImageUrl,
                    variant.Price ?? product.Price,
                    originalPrice,
                    finalPrice,
                    percent,
                    variant.Stock,
                    variant.IsActive,
                    ToBadges(badges, variant.Id));
            })
            .ToList();
    }

    public async Task<CatalogVariant?> GetVariantAsync(
        int productId,
        int variantId,
        CancellationToken cancellationToken = default)
    {
        var variants = await GetVariantsAsync(productId, cancellationToken);

        return variants?.FirstOrDefault(variant => variant.Id == variantId);
    }

    private async Task<List<CatalogProduct>> BuildProductsAsync(
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(product => product.Id).ToList();
        var now = DateTime.UtcNow;

        var discounts = await discountService.GetActiveForProductsAsync(
            productIds,
            now,
            cancellationToken);

        var badges = await LoadBadgesAsync(productIds, now, cancellationToken);

        return products
            .Select(product =>
            {
                var (originalPrice, finalPrice, percent) = ProductPricing.Resolve(
                    product,
                    variant: null,
                    discounts,
                    now);

                return new CatalogProduct(
                    product.Id,
                    product.CategoryId,
                    product.Name,
                    product.Description,
                    product.Price,
                    originalPrice,
                    finalPrice,
                    percent,
                    product.ImageUrl,
                    product.Stock,
                    product.IsActive,
                    product.IsDigital,
                    product.CreatedAt,
                    product.UpdatedAt,
                    ToProductBadges(badges, product.Id));
            })
            .ToList();
    }

    private static IReadOnlyList<CatalogBadge> ToProductBadges(
        IReadOnlyList<ProductBadgeAssignment> assignments,
        int productId)
    {
        return assignments
            .Where(assignment => assignment.ProductId == productId)
            .Select(assignment => new CatalogBadge(
                assignment.Badge.Id,
                assignment.Badge.Code,
                assignment.Badge.Name,
                assignment.Badge.Color,
                assignment.Badge.SortOrder,
                ProductVariantId: null))
            .DistinctBy(badge => badge.Id)
            .ToList();
    }

    private async Task<List<ProductBadgeAssignment>> LoadBadgesAsync(
        IReadOnlyCollection<int> productIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.ProductBadgeAssignments
            .AsNoTracking()
            .Include(assignment => assignment.Badge)
                .ThenInclude(badge => badge.Settings)
            .Where(assignment => productIds.Contains(assignment.ProductId))
            .ToListAsync(cancellationToken);

        return assignments
            .Where(assignment =>
                assignment.Badge.IsActive &&
                (!string.Equals(
                     assignment.Badge.AssignmentType,
                     ProductBadgeAssignmentTypes.Automatic,
                     StringComparison.OrdinalIgnoreCase) ||
                 (assignment.Badge.Settings?.IsEnabled ?? false)) &&
                (assignment.StartsAt is null || assignment.StartsAt <= now) &&
                (assignment.EndsAt is null || assignment.EndsAt > now))
            .OrderBy(assignment => assignment.Badge.SortOrder)
            .ThenBy(assignment => assignment.Badge.Id)
            .ToList();
    }

    private static IReadOnlyList<CatalogBadge> ToBadges(
        IReadOnlyList<ProductBadgeAssignment> assignments,
        int? productVariantId,
        int? productId = null)
    {
        return assignments
            .Where(assignment =>
                (productId is null || assignment.ProductId == productId) &&
                (productVariantId is null
                    ? assignment.ProductVariantId is null
                    : assignment.ProductVariantId is null ||
                      assignment.ProductVariantId == productVariantId))
            .Select(assignment => new CatalogBadge(
                assignment.Badge.Id,
                assignment.Badge.Code,
                assignment.Badge.Name,
                assignment.Badge.Color,
                assignment.Badge.SortOrder,
                assignment.ProductVariantId))
            .GroupBy(badge => badge.Id)
            .Select(group => group
                .OrderByDescending(badge => badge.ProductVariantId is not null)
                .First())
            .ToList();
    }
}
