using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Repositories;

public interface IBalanceRepository
{
    Task<Balance?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Balance balance, CancellationToken cancellationToken = default);

    void Update(Balance balance);
}
