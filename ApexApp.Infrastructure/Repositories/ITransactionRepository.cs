using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
