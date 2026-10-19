using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Repositories;

public sealed class TransactionRepository(AppDbContext dbContext) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.SingleOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(
        Guid userId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        dbContext.Transactions.AddAsync(transaction, cancellationToken).AsTask();
}
