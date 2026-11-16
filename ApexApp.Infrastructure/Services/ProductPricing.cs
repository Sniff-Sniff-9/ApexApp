using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Pure pricing rules shared by the catalog, cart and checkout. Keeping the
/// selection and rounding here guarantees that all three screens agree.
/// </summary>
public static class ProductPricing
{
    /// <summary>
    /// Selects the effective discount for a product/variant at a point in time.
    /// A variant-specific discount overrides a product-wide one; within the same
    /// scope the highest percentage wins, then the newest row.
    /// </summary>
    public static ProductDiscount? SelectEffective(
        IEnumerable<ProductDiscount> discounts,
        int? variantId,
        DateTime at)
    {
        return discounts
            .Where(discount =>
                discount.IsActive &&
                discount.StartsAt <= at &&
                (discount.EndsAt is null || discount.EndsAt > at) &&
                (discount.ProductVariantId is null ||
                 (variantId is not null && discount.ProductVariantId == variantId)))
            .OrderByDescending(discount => discount.ProductVariantId is not null)
            .ThenByDescending(discount => discount.DiscountPercent)
            .ThenByDescending(discount => discount.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Applies an integer percentage with commercial rounding (half away from zero).
    /// </summary>
    public static long ApplyDiscount(long price, int discountPercent)
    {
        if (price <= 0 || discountPercent <= 0)
        {
            return price;
        }

        if (discountPercent >= 100)
        {
            return 0;
        }

        var percent = Math.Clamp(discountPercent, 0, 100);
        var discounted = price * (100m - percent) / 100m;

        return checked((long)Math.Round(discounted, 0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Returns base price, final price and applied percentage for a product/variant.
    /// </summary>
    public static (long OriginalPrice, long FinalPrice, int? DiscountPercent) Resolve(
        Product product,
        ProductVariant? variant,
        IEnumerable<ProductDiscount> discounts,
        DateTime at)
    {
        var originalPrice = variant?.Price ?? product.Price;
        var discount = SelectEffective(discounts, variant?.Id, at);

        return discount is null
            ? (originalPrice, originalPrice, null)
            : (originalPrice, ApplyDiscount(originalPrice, discount.DiscountPercent), discount.DiscountPercent);
    }
}
