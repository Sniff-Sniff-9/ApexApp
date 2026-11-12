using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class ProductService(
    ProductRepository repository,
    ICacheService cache) : IProductService
{
    public async Task<List<Product>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Products(categoryId);

        if (cache.TryGet<List<Product>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var products = await repository.GetActiveAsync(
            categoryId, cancellationToken);

        cache.Set(cacheKey, products, CacheKeys.ProductsTtl);

        return products;
    }

    public async Task<Product?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var cacheKey = CacheKeys.Product(id);

        if (cache.TryGet<Product>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var product = await repository.GetActiveByIdAsync(
            id, cancellationToken);

        if (product is not null)
        {
            cache.Set(cacheKey, product, CacheKeys.ProductsTtl);
        }

        return product;
    }

    public async Task<(bool Success, string? Error, Product? Data)> CreateAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        var error = await ValidateProductAsync(product, cancellationToken);

        if (error is not null)
            return (false, error, null);

        var now = DateTime.UtcNow;

        product.Name = product.Name.Trim();
        product.CreatedAt = now;
        product.UpdatedAt = now;

        await repository.AddAsync(product, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);

        return (true, null, product);
    }

    public async Task<(bool Success, string? Error, Product? Data)> UpdateAsync(
        int id,
        Product input,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return (false, "Invalid product ID.", null);

        var error = await ValidateProductAsync(input, cancellationToken);

        if (error is not null)
            return (false, error, null);

        var product = await repository.GetByIdAsync(id, cancellationToken);

        if (product is null)
            return (false, "Product was not found.", null);

        product.CategoryId = input.CategoryId;
        product.Name = input.Name.Trim();
        product.Description = input.Description;
        product.Price = input.Price;
        product.ImageUrl = input.ImageUrl;
        product.Stock = input.Stock;
        product.IsActive = input.IsActive;
        product.IsDigital = input.IsDigital;
        product.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);

        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);

        return (true, null, product);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return (false, "Invalid product ID.");

        var product = await repository.GetByIdAsync(id, cancellationToken);

        if (product is null)
            return (false, "Product was not found.");

        // Soft delete: preserve order history and foreign-key references.
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);

        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);

        return (true, null);
    }

    private async Task<string?> ValidateProductAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return "Product name is required.";

        if (product.Price < 0)
            return "Price cannot be negative.";

        if (product.Stock < 0)
            return "Stock cannot be negative.";

        if (product.CategoryId.HasValue)
        {
            var exists = await repository.CategoryExistsAsync(
                product.CategoryId.Value, cancellationToken);

            if (!exists)
                return "Category was not found.";
        }

        if (product.IsDigital)
            return "Digital products are not supported yet.";

        return null;
    }

    public async Task<List<Product>> GetAllAsync(
    int? categoryId = null,
    CancellationToken cancellationToken = default)
    {
        return await repository.GetAllAsync(
            categoryId,
            cancellationToken);
    }

    public async Task<IEnumerable<ProductVariant>> GetVariantsAsync(int productId)
    {
        var product = await repository.GetByIdAsync(productId);

        if (product == null)
            throw new KeyNotFoundException("Product not found.");

        return await repository.GetVariantsAsync(productId);
    }

    public async Task<ProductVariant?> GetVariantByIdAsync(int productId, int variantId)
    {
        var product = await repository.GetByIdAsync(productId);

        if (product == null)
            throw new KeyNotFoundException("Product not found.");

        return await repository.GetVariantByIdAsync(productId, variantId);
    }

    public async Task<ProductVariant> AddVariantAsync(int productId, ProductVariant variant)
    {
        var product = await repository.GetByIdAsync(productId);

        if (product == null)
            throw new KeyNotFoundException("Product not found.");

        if (string.IsNullOrWhiteSpace(variant.Name))
            throw new ArgumentException("Variant name is required.");

        if (variant.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        if (variant.Price < 0)
            throw new ArgumentException("Price cannot be negative.");

        variant.ProductId = productId;
        variant.IsActive = true;

        return await repository.AddVariantAsync(variant);
    }

    public async Task<ProductVariant?> UpdateVariantAsync(int productId, int variantId, ProductVariant variant)
    {
        var existingVariant = await repository.GetVariantByIdAsync(productId, variantId);

        if (existingVariant == null)
            return null;

        if (string.IsNullOrWhiteSpace(variant.Name))
            throw new ArgumentException("Variant name is required.");

        if (variant.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        if (variant.Price < 0)
            throw new ArgumentException("Price cannot be negative.");

        existingVariant.Name = variant.Name;
        // Присваивается идентификатор категории: сущность категории не загружена,
        // а присваивание навигации обнуляло категорию при каждом обновлении.
        existingVariant.CategoryId = variant.CategoryId;
        existingVariant.ImageUrl = variant.ImageUrl;
        existingVariant.Price = variant.Price;
        existingVariant.Stock = variant.Stock;
        existingVariant.IsActive = variant.IsActive;

        return await repository.UpdateVariantAsync(existingVariant);
    }

    public async Task<bool> DeleteVariantAsync(int productId, int variantId)
    {
        var variant = await repository.GetVariantByIdAsync(productId, variantId);

        if (variant == null)
            return false;

        try
        {
            return await repository.DeleteVariantAsync(variant);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            // Вариант уже продан или лежит в корзине: физическое удаление нарушило бы историю.
            throw new InvalidOperationException(
                "Cannot delete a variant that is referenced by carts or orders.");
        }
    }
}