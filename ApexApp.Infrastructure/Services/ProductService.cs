using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Repositories;

namespace ApexApp.Infrastructure.Services;

public sealed class ProductService(
    ProductRepository repository) : IProductService
{
    public Task<List<Product>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        return repository.GetActiveAsync(
            categoryId, cancellationToken);
    }

    public Task<Product?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Task.FromResult<Product?>(null);

        return repository.GetActiveByIdAsync(
            id, cancellationToken);
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

}