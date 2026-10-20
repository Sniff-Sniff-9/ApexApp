using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface ICartService
{
    Task<List<CartItem>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> AddAsync(
        Guid userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> UpdateAsync(
        Guid userId,
        long itemId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> RemoveAsync(
        Guid userId,
        long itemId,
        CancellationToken cancellationToken = default);
}