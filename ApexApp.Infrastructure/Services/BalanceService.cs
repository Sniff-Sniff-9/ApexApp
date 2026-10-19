using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;

namespace ApexApp.Infrastructure.Services;

public sealed class BalanceService(
    AppDbContext dbContext,
    IBalanceRepository balanceRepository,
    ITransactionRepository transactionRepository) : IBalanceService
{
    public async Task<ServiceResult<Balance>> AdjustBalanceAsync(
        Guid userId,
        long amount,
        string? description = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<Balance>.Fail(400, "A valid user ID is required.");
        }

        if (amount == 0)
        {
            return ServiceResult<Balance>.Fail(400, "The adjustment amount cannot be zero.");
        }

        await using var databaseTransaction = dbContext.Database
            .BeginTransaction(System.Data.IsolationLevel.Serializable, );

        var balance = await balanceRepository.GetByUserIdAsync(userId, cancellationToken);
        var isNewBalance = balance is null;
        balance ??= new Balance
        {
            UserId = userId,
            Amount = 0,
            Version = 0,
            UpdatedAt = DateTime.UtcNow
        };

        long adjustedAmount;
        try
        {
            adjustedAmount = checked(balance.Amount + amount);
            balance.Version = checked(balance.Version + 1);
        }
        catch (OverflowException)
        {
            return ServiceResult<Balance>.Fail(400, "The adjustment exceeds the supported balance range.");
        }

        if (adjustedAmount < 0)
        {
            return ServiceResult<Balance>.Fail(409, "The adjustment would make the balance negative.");
        }

        balance.Amount = adjustedAmount;
        balance.UpdatedAt = DateTime.UtcNow;

        if (isNewBalance)
        {
            await balanceRepository.AddAsync(balance, cancellationToken);
        }
        else
        {
            balanceRepository.Update(balance);
        }

        await transactionRepository.AddAsync(new Transaction
        {
            UserId = userId,
            Amount = amount,
            BalanceAfter = adjustedAmount,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return ServiceResult<Balance>.Ok(balance);
    }
}
