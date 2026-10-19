using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

public interface IBalanceService
{
    Task<ServiceResult<Balance>> AdjustBalanceAsync(
        Guid userId,
        BalanceType balanceType,
        long amount,
        TransactionType transactionType,
        Guid? relatedUserId = null,
        string? relatedEntityType = null,
        long? relatedEntityId = null,
        string? description = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default);
}
