using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>Administrator-facing product discount operations.</summary>
public interface IProductDiscountService
{
    Task<List<ProductDiscount>> GetForProductAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDiscount>> GetActiveForProductsAsync(
        IReadOnlyCollection<int> productIds,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDiscount>> CreateAsync(
        int productId,
        ProductDiscountInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDiscount>> UpdateAsync(
        long id,
        ProductDiscountInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default);
}
