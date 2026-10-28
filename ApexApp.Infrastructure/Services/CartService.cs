using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

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
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || productId <= 0 || quantity <= 0)
            return (false, "Invalid user, product, or quantity.");

        var product = await dbContext.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == productId &&
                     p.IsActive &&
                     !p.IsDigital,
                cancellationToken);

        if (product is null)
            return (false, "Product is unavailable.");

        var item = await cartRepository.GetByUserAndProductAsync(
            userId, productId, cancellationToken);

        var newQuantity = (long)(item?.Quantity ?? 0) + quantity;

        if (newQuantity > int.MaxValue)
            return (false, "Quantity exceeds the supported range.");

        if (product.Stock.HasValue &&
            newQuantity > product.Stock.Value)
        {
            return (false, "Requested quantity exceeds available stock.");
        }

        if (item is null)
        {
            await cartRepository.AddAsync(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                Quantity = quantity,
                AddedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            item.Quantity = (int)newQuantity;
        }

        await cartRepository.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Guid userId,
        long itemId,
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
            .FirstOrDefaultAsync(
                p => p.Id == item.ProductId &&
                     p.IsActive &&
                     !p.IsDigital,
                cancellationToken);

        if (product is null)
            return (false, "Product is unavailable.");

        if (product.Stock.HasValue && quantity > product.Stock.Value)
            return (false, "Requested quantity exceeds available stock.");

        item.Quantity = quantity;

        await cartRepository.SaveChangesAsync(cancellationToken);

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
}