using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Repositories;

public sealed class ProductRepository(
    AppDbContext dbContext) 
{
    public Task<List<Product>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<Product>()
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDigital);

        if (categoryId.HasValue)
        {
            query = query.Where(
                p => p.CategoryId == categoryId.Value);
        }

        return query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Product?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == id &&
                     p.IsActive &&
                     !p.IsDigital,
                cancellationToken);
    }

    public Task<Product?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<bool> CategoryExistsAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Category>()
            .AnyAsync(c => c.Id == categoryId, cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Product>()
            .AddAsync(product, cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Product>> GetAllAsync(
    int? categoryId = null,
    CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<Product>()
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        return await query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}