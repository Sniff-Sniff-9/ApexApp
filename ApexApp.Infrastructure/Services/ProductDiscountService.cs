using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Administrator-managed product discounts. The service is the only place that
/// validates discount windows and product/variant ownership.
/// </summary>
public sealed class ProductDiscountService(
    AppDbContext dbContext,
    ICacheService cache) : IProductDiscountService
{
    private const int MinDiscountPercent = 1;
    private const int MaxDiscountPercent = 99;

    public Task<List<ProductDiscount>> GetForProductAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ProductDiscounts
            .AsNoTracking()
            .Where(discount => discount.ProductId == productId)
            .OrderByDescending(discount => discount.StartsAt)
            .ThenByDescending(discount => discount.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDiscount>> GetActiveForProductsAsync(
        IReadOnlyCollection<int> productIds,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ProductDiscounts
            .AsNoTracking()
            .Where(discount =>
                productIds.Contains(discount.ProductId) &&
                discount.IsActive &&
                discount.StartsAt <= at &&
                (discount.EndsAt == null || discount.EndsAt > at))
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<ProductDiscount>> CreateAsync(
        int productId,
        ProductDiscountInput input,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return ServiceResult<ProductDiscount>.Fail(400, "A valid product ID is required.");
        }

        if (input is null)
        {
            return ServiceResult<ProductDiscount>.Fail(400, "A discount payload is required.");
        }

        var validationError = await ValidateAsync(productId, input, cancellationToken);

        if (validationError is not null)
        {
            return ServiceResult<ProductDiscount>.Fail(400, validationError);
        }

        var now = DateTime.UtcNow;

        var discount = new ProductDiscount
        {
            ProductId = productId,
            ProductVariantId = input.ProductVariantId,
            DiscountPercent = input.DiscountPercent,
            StartsAt = UtcDateTime.Normalize(input.StartsAt),
            EndsAt = UtcDateTime.Normalize(input.EndsAt),
            IsActive = input.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProductDiscounts.Add(discount);
        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        return ServiceResult<ProductDiscount>.Ok(discount);
    }

    public async Task<ServiceResult<ProductDiscount>> UpdateAsync(
        long id,
        ProductDiscountInput input,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ServiceResult<ProductDiscount>.Fail(400, "A valid discount ID is required.");
        }

        if (input is null)
        {
            return ServiceResult<ProductDiscount>.Fail(400, "A discount payload is required.");
        }

        var discount = await dbContext.ProductDiscounts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (discount is null)
        {
            return ServiceResult<ProductDiscount>.Fail(404, "Discount was not found.");
        }

        var validationError = await ValidateAsync(
            discount.ProductId,
            input,
            cancellationToken);

        if (validationError is not null)
        {
            return ServiceResult<ProductDiscount>.Fail(400, validationError);
        }

        discount.ProductVariantId = input.ProductVariantId;
        discount.DiscountPercent = input.DiscountPercent;
        discount.StartsAt = UtcDateTime.Normalize(input.StartsAt);
        discount.EndsAt = UtcDateTime.Normalize(input.EndsAt);
        discount.IsActive = input.IsActive;
        discount.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        return ServiceResult<ProductDiscount>.Ok(discount);
    }

    public async Task<ServiceResult> DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ServiceResult.Fail(400, "A valid discount ID is required.");
        }

        var discount = await dbContext.ProductDiscounts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (discount is null)
        {
            return ServiceResult.Fail(404, "Discount was not found.");
        }

        if (discount.IsActive)
        {
            discount.IsActive = false;
            discount.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();
        }

        return ServiceResult.Ok();
    }

    private async Task<string?> ValidateAsync(
        int productId,
        ProductDiscountInput input,
        CancellationToken cancellationToken)
    {
        if (input.DiscountPercent is < MinDiscountPercent or > MaxDiscountPercent)
        {
            return $"Discount percent must be between {MinDiscountPercent} and {MaxDiscountPercent}.";
        }

        if (input.EndsAt is not null &&
            UtcDateTime.Normalize(input.EndsAt.Value) <= UtcDateTime.Normalize(input.StartsAt))
        {
            return "EndsAt must be later than StartsAt.";
        }

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken);

        if (!productExists)
        {
            return "Product was not found.";
        }

        if (input.ProductVariantId is not int variantId)
        {
            return null;
        }

        if (variantId <= 0)
        {
            return "A valid product variant ID is required.";
        }

        var variantExists = await dbContext.ProductVariants
            .AsNoTracking()
            .AnyAsync(
                variant => variant.Id == variantId && variant.ProductId == productId,
                cancellationToken);

        return variantExists
            ? null
            : "Product variant was not found for this product.";
    }

    private void InvalidateCatalog()
    {
        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);
        cache.RemoveByPrefix(CacheKeys.CatalogPrefix);
    }
}
