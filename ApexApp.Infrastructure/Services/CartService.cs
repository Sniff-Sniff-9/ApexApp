using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Корзина пользователя. Цена и остаток всегда вычисляются по данным БД:
/// клиент передаёт только идентификаторы товара и варианта, но не цену.
/// </summary>
public sealed class CartService(
    CartRepository cartRepository,
    AppDbContext dbContext) : ICartService
{
    public Task<List<CartItem>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return cartRepository.GetByUserIdAsync(
            userId, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> AddAsync(
        Guid userId,
        int productId,
        int? variantId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || productId <= 0 || quantity <= 0)
            return (false, "Invalid user, product, or quantity.");

        var product = await dbContext.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return (false, "Product is unavailable.");

        var (variant, variantError) = await LoadVariantAsync(
            productId,
            variantId,
            cancellationToken);

        if (variantError is not null)
            return (false, variantError);

        var item = await cartRepository.GetByUserAndProductAsync(
            userId, productId, cancellationToken);

        // Ограничение корзины допускает одну строку на товар, поэтому смена варианта
        // должна выполняться через обновление строки, а не молчаливой перезаписью.
        if (item is not null && item.ProductVariantId != variant?.Id)
        {
            return (false,
                "The cart already contains this product with a different variant selection. " +
                "Update or remove the existing item first.");
        }

        var newQuantity = (long)(item?.Quantity ?? 0) + quantity;

        if (newQuantity > int.MaxValue)
            return (false, "Quantity exceeds the supported range.");

        var selection = ProductVariantPricing.Evaluate(product, variant, (int)newQuantity);

        if (!selection.IsValid)
            return (false, Describe(selection.Status));

        if (item is null)
        {
            await cartRepository.AddAsync(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                ProductVariantId = variant?.Id,
                Quantity = quantity,
                AddedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            item.Quantity = (int)newQuantity;
        }

        try
        {
            await cartRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
        {
            return (false, "The cart was updated concurrently. Please retry.");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Guid userId,
        long itemId,
        int? variantId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || itemId <= 0 || quantity <= 0)
            return (false, "Invalid item ID or quantity.");

        var item = await cartRepository.GetByIdAsync(
            itemId, cancellationToken);

        if (item is null || item.UserId != userId)
            return (false, "Cart item was not found.");

        var product = await dbContext.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

        if (product is null)
            return (false, "Product is unavailable.");

        // Отсутствие variantId в запросе означает «оставить текущий вариант».
        var targetVariantId = variantId ?? item.ProductVariantId;

        var (variant, variantError) = await LoadVariantAsync(
            product.Id,
            targetVariantId,
            cancellationToken);

        if (variantError is not null)
            return (false, variantError);

        var selection = ProductVariantPricing.Evaluate(product, variant, quantity);

        if (!selection.IsValid)
            return (false, Describe(selection.Status));

        item.Quantity = quantity;
        item.ProductVariantId = variant?.Id;

        try
        {
            await cartRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
        {
            return (false, "The cart was updated concurrently. Please retry.");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveAsync(
        Guid userId,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || itemId <= 0)
            return (false, "Invalid item ID.");

        var item = await cartRepository.GetByIdAsync(
            itemId, cancellationToken);

        if (item is null || item.UserId != userId)
            return (false, "Cart item was not found.");

        cartRepository.Remove(item);

        await cartRepository.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    private async Task<(ProductVariant? Variant, string? Error)> LoadVariantAsync(
        int productId,
        int? variantId,
        CancellationToken cancellationToken)
    {
        if (variantId is not int id)
        {
            return (null, null);
        }

        if (id <= 0)
        {
            return (null, "Variant was not found for this product.");
        }

        var variant = await dbContext.Set<ProductVariant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        // Проверка принадлежности обязательна: идентификатор варианта приходит от клиента.
        if (variant is null || variant.ProductId != productId)
        {
            return (null, "Variant was not found for this product.");
        }

        return (variant, null);
    }

    private static string Describe(ProductSelectionStatus status) => status switch
    {
        ProductSelectionStatus.QuantityInvalid => "Invalid quantity.",
        ProductSelectionStatus.VariantUnavailable => "Variant is unavailable for this product.",
        ProductSelectionStatus.InsufficientStock => "Requested quantity exceeds available stock.",
        _ => "Product is unavailable."
    };
}
