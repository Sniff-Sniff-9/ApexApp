using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class BalanceService(
    AppDbContext dbContext,
    IBalanceRepository balanceRepository,
    ITransactionRepository transactionRepository) : IBalanceService
{
    public async Task<ServiceResult<Balance>> AdjustBalanceAsync(
        Guid userId,
        BalanceType balanceType,
        long amount,
        TransactionType transactionType,
        Guid? relatedUserId = null,
        string? relatedEntityType = null,
        long? relatedEntityId = null,
        string? description = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<Balance>.Fail(400, "A valid user ID is required.");
        }

        var userExists = await dbContext.Users
    .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
        {
            return ServiceResult<Balance>.Fail(404, "User was not found.");
        }

        var relatedUserExists = await dbContext.Users
    .AnyAsync(u => u.Id == relatedUserId, cancellationToken);

        if (!relatedUserExists)
        {
            return ServiceResult<Balance>.Fail(404, "Related user was not found.");
        }

        if (amount == 0)
        {
            return ServiceResult<Balance>.Fail(400, "The adjustment amount cannot be zero.");
        }

        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken );

        var balance = await balanceRepository.GetByUserIdAsync(userId, balanceType, cancellationToken);
        var isNewBalance = balance is null;
        balance ??= new Balance
        {
            UserId = userId,
            Type = balanceType,
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
            Type = transactionType,
            Amount = amount,
            BalanceAfter = adjustedAmount,
            Description = description,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            RelatedUserId = relatedUserId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return ServiceResult<Balance>.Ok(balance);
    }
}
