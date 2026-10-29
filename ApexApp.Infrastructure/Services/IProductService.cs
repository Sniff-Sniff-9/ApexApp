using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface IProductService
{
    Task<List<Product>> GetActiveAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<Product?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, Product? Data)> CreateAsync(
    Product product,
    CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, Product? Data)> UpdateAsync(
        int id,
        Product product,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<Product>> GetAllAsync(
    int? categoryId = null,
    CancellationToken cancellationToken = default);
}