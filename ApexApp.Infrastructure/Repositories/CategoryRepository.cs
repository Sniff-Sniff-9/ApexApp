using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Repositories;

public sealed class CategoryRepository(
    AppDbContext dbContext)
{

    public Task<List<Category>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Category>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Category>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id,
                cancellationToken);
    }

    public Task<bool> HasProductsAsync(
    int categoryId,
    CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Product>()
            .AnyAsync(p => p.CategoryId == categoryId, cancellationToken);
    }

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Category>()
            .AddAsync(category, cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Remove(Category category)
    {
        dbContext.Set<Category>().Remove(category);
    }
}