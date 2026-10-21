using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface IOrderService
{
    Task<(bool Success, int StatusCode, string? Error, Order? Data, Achievement? NewAchievement)>
        CheckoutAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

    Task<List<Order>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(
        Guid userId,
        long orderId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, int StatusCode, string? Error, Order? Data)>
    UpdateStatusAsync(
        long orderId,
        Guid employeeId,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default);
}