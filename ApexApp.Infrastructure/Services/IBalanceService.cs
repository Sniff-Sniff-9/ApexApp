using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

public interface IBalanceService
{
    Task<ServiceResult<Balance>> AdjustBalanceAsync(
        Guid userId,
        long amount,
        string? description = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default);
}
