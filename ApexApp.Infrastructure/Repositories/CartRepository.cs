using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Repositories;

public sealed class CartRepository(
    AppDbContext dbContext) 
{
    public Task<List<CartItem>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<CartItem>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
            .OrderBy(x => x.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<CartItem?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<CartItem>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<CartItem?> GetByUserProductAndVariantAsync(
        Guid userId,
        int productId,
        int? variantId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<CartItem>()
            .Where(x => x.UserId == userId && x.ProductId == productId);

        // Null — самостоятельный выбор варианта, поэтому ищем строку без варианта,
        // а не любую строку этого товара.
        if (variantId is not int id)
        {
            return query.FirstOrDefaultAsync(
                x => x.ProductVariantId == null,
                cancellationToken);
        }

        return query.FirstOrDefaultAsync(
            x => x.ProductVariantId == id,
            cancellationToken);
    }

    public async Task AddAsync(
        CartItem item,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Set<CartItem>().AddAsync(item, cancellationToken);
    }

    public void Remove(CartItem item)
    {
        dbContext.Set<CartItem>().Remove(item);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}