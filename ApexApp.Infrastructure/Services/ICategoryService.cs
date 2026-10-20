using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface ICategoryService
{
    Task<List<Category>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, Category? Data)> CreateAsync(
    string name,
    int sortOrder,
    CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error, Category? Data)> UpdateAsync(
        int id,
        string name,
        int sortOrder,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}