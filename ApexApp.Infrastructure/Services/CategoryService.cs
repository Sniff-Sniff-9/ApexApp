using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Repositories;

namespace ApexApp.Infrastructure.Services;

public sealed class CategoryService(
    CategoryRepository repository,
    ICacheService cache) : ICategoryService
{
    public async Task<List<Category>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGet<List<Category>>(CacheKeys.Categories, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var categories = await repository.GetAllAsync(cancellationToken);

        cache.Set(CacheKeys.Categories, categories, CacheKeys.CategoriesTtl);

        return categories;
    }

    public Task<Category?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Task.FromResult<Category?>(null);

        return repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<(bool Success, string? Error, Category? Data)> CreateAsync(
        string name,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Category name is required.", null);

        var category = new Category
        {
            Name = name,
            SortOrder = sortOrder
        };

        await repository.AddAsync(category, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);

        return (true, null, category);
    }

    public async Task<(bool Success, string? Error, Category? Data)> UpdateAsync(
        int id,
        string name,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return (false, "Invalid category ID.", null);

        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Category name is required.", null);

        var category = await repository.GetByIdAsync(id, cancellationToken);

        if (category is null)
            return (false, "Category was not found.", null);

        category.Name = name;
        category.SortOrder = sortOrder;

        await repository.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);

        return (true, null, category);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return (false, "Invalid category ID.");

        var category = await repository.GetByIdAsync(id, cancellationToken);

        if (category is null)
            return (false, "Category was not found.");

        if (await repository.HasProductsAsync(id, cancellationToken))
            return (false, "Cannot delete a category that contains products.");

        repository.Remove(category);
        await repository.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);

        return (true, null);
    }
}