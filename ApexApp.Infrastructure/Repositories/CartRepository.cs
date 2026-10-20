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

    public Task<CartItem?> GetByUserAndProductAsync(
        Guid userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<CartItem>()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.ProductId == productId,
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