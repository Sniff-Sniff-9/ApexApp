using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Начисляет награду за активность на личный баланс пользователя
/// и фиксирует соответствующую транзакцию.
/// </summary>
/// <remarks>
/// В отличие от <see cref="IBalanceService"/>, начисление не связано с другим пользователем
/// (<c>related_user_id</c> всегда <c>null</c>), поэтому проверка связанного пользователя не выполняется.
/// </remarks>
public sealed class ActivityRewardGranter(
    AppDbContext dbContext,
    IBalanceRepository balanceRepository,
    ITransactionRepository transactionRepository)
{
    /// <summary>
    /// Начисляет награду на личный баланс.
    /// Если вызывающий код уже открыл транзакцию БД, начисление выполняется в её рамках,
    /// иначе создаётся собственная транзакция уровня <see cref="IsolationLevel.Serializable"/>.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="amount">Сумма награды (должна быть больше нуля).</param>
    /// <param name="description">Описание транзакции.</param>
    /// <param name="relatedEntityType">Тип сущности-источника награды.</param>
    /// <param name="relatedEntityId">Идентификатор сущности-источника награды.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task<ServiceResult<Balance>> GrantAsync(
        Guid userId,
        long amount,
        string description,
        string relatedEntityType,
        long relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var ambientCredit = await CreditAsync(
                userId,
                amount,
                description,
                relatedEntityType,
                relatedEntityId,
                cancellationToken);

            if (!ambientCredit.IsSuccess)
            {
                return ambientCredit;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return ambientCredit;
        }

        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var credit = await CreditAsync(
            userId,
            amount,
            description,
            relatedEntityType,
            relatedEntityId,
            cancellationToken);

        if (!credit.IsSuccess)
        {
            await databaseTransaction.RollbackQuietlyAsync();

            return credit;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return credit;
    }

    /// <summary>
    /// Начисляет награду на личный баланс пользователя, существование которого уже
    /// гарантировано внешним ключом вызывающей сущности (участие в квесте, участие
    /// в мероприятии, ответ на опрос), поэтому проверка пользователя не выполняется.
    /// Если вызывающий код уже открыл транзакцию БД, начисление выполняется в её рамках,
    /// иначе создаётся собственная транзакция уровня <see cref="IsolationLevel.Serializable"/>.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="amount">Сумма награды (должна быть больше нуля).</param>
    /// <param name="description">Описание транзакции.</param>
    /// <param name="relatedEntityType">Тип сущности-источника награды.</param>
    /// <param name="relatedEntityId">Идентификатор сущности-источника награды.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task<ServiceResult<Balance>> GrantForKnownUserAsync(
        Guid userId,
        long amount,
        string description,
        string relatedEntityType,
        long relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var ambientCredit = await CreditKnownUserAsync(
                userId,
                amount,
                description,
                relatedEntityType,
                relatedEntityId,
                cancellationToken);

            if (!ambientCredit.IsSuccess)
            {
                return ambientCredit;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return ambientCredit;
        }

        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var credit = await CreditKnownUserAsync(
            userId,
            amount,
            description,
            relatedEntityType,
            relatedEntityId,
            cancellationToken);

        if (!credit.IsSuccess)
        {
            await databaseTransaction.RollbackQuietlyAsync();

            return credit;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return credit;
    }

    /// <summary>
    /// Подготавливает начисление награды: изменяет баланс и добавляет транзакцию
    /// в контекст. Сохранение изменений выполняет вызывающий код.
    /// </summary>
    public async Task<ServiceResult<Balance>> CreditAsync(
        Guid userId,
        long amount,
        string description,
        string relatedEntityType,
        long relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<Balance>.Fail(400, "A valid user ID is required.");
        }

        if (amount <= 0)
        {
            return ServiceResult<Balance>.Fail(400, "The reward amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(relatedEntityType))
        {
            return ServiceResult<Balance>.Fail(400, "A related entity type is required.");
        }

        var userExists = await dbContext.Users
            .AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
        {
            return ServiceResult<Balance>.Fail(404, "User was not found.");
        }

        return await ApplyCreditAsync(
            userId,
            amount,
            description,
            relatedEntityType,
            relatedEntityId,
            cancellationToken);
    }

    /// <summary>
    /// Подготавливает начисление награды для пользователя, существование которого
    /// уже гарантировано внешним ключом вызывающей сущности.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="amount">Сумма награды (должна быть больше нуля).</param>
    /// <param name="description">Описание транзакции.</param>
    /// <param name="relatedEntityType">Тип сущности-источника награды.</param>
    /// <param name="relatedEntityId">Идентификатор сущности-источника награды.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task<ServiceResult<Balance>> CreditKnownUserAsync(
        Guid userId,
        long amount,
        string description,
        string relatedEntityType,
        long relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<Balance>.Fail(400, "A valid user ID is required.");
        }

        if (amount <= 0)
        {
            return ServiceResult<Balance>.Fail(400, "The reward amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(relatedEntityType))
        {
            return ServiceResult<Balance>.Fail(400, "A related entity type is required.");
        }

        return await ApplyCreditAsync(
            userId,
            amount,
            description,
            relatedEntityType,
            relatedEntityId,
            cancellationToken);
    }

    /// <summary>
    /// Изменяет баланс и добавляет транзакцию в контекст;
    /// проверки входных данных выполняет вызывающий код.
    /// </summary>
    private async Task<ServiceResult<Balance>> ApplyCreditAsync(
        Guid userId,
        long amount,
        string description,
        string relatedEntityType,
        long relatedEntityId,
        CancellationToken cancellationToken)
    {
        var balance = await balanceRepository.GetByUserIdAsync(userId, BalanceType.Personal, cancellationToken);
        var isNewBalance = balance is null;

        balance ??= new Balance
        {
            UserId = userId,
            Type = BalanceType.Personal,
            Amount = 0,
            Version = 0,
            UpdatedAt = DateTime.UtcNow
        };

        long adjustedAmount;
        int nextVersion;

        try
        {
            adjustedAmount = checked(balance.Amount + amount);
            nextVersion = checked(balance.Version + 1);
        }
        catch (OverflowException)
        {
            return ServiceResult<Balance>.Fail(400, "The reward exceeds the supported balance range.");
        }

        balance.Amount = adjustedAmount;
        balance.Version = nextVersion;
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
            Type = TransactionType.ActivityReward,
            Amount = amount,
            BalanceAfter = adjustedAmount,
            Description = description,
            RelatedUserId = null,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedBy = null,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return ServiceResult<Balance>.Ok(balance);
    }
}
