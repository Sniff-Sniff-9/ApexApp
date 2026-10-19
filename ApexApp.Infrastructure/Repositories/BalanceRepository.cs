using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Repositories;

public sealed class BalanceRepository(AppDbContext dbContext) : IBalanceRepository
{
    public Task<Balance?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Balances.SingleOrDefaultAsync(balance => balance.UserId == userId, cancellationToken);

    public Task AddAsync(Balance balance, CancellationToken cancellationToken = default) =>
        dbContext.Balances.AddAsync(balance, cancellationToken).AsTask();

    public void Update(Balance balance) => dbContext.Balances.Update(balance);
}
