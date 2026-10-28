using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class AchievementService(
    AppDbContext dbContext,
    ICacheService cache) : IAchievementService
{
    public async Task<IReadOnlyList<Achievement>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGet<IReadOnlyList<Achievement>>(
                CacheKeys.Achievements, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var achievements = await dbContext.Achievements
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        cache.Set<IReadOnlyList<Achievement>>(
            CacheKeys.Achievements, achievements, CacheKeys.AchievementsTtl);

        return achievements;
    }

    public async Task<Achievement?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Achievement> CreateAsync(
        Achievement achievement,
        CancellationToken cancellationToken = default)
    {
        achievement.Code = achievement.Code.Trim().ToUpperInvariant();
        achievement.Name = achievement.Name.Trim();

        dbContext.Achievements.Add(achievement);
        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Achievements);

        return achievement;
    }

    public async Task<bool> UpdateAsync(
        int id,
        string code,
        string name, string? description, long rewardAmount, bool isActive,
        CancellationToken cancellationToken = default)
    {
        var achievement = await dbContext.Achievements
            .FirstOrDefaultAsync(a => a.Code == code, cancellationToken);

        if (achievement is null)
            return false;

        achievement.Code = code.Trim().ToUpperInvariant();
        achievement.Name = name.Trim();
        achievement.Description = description;
        achievement.RewardAmount = rewardAmount;
        achievement.IsActive = isActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Achievements);

        return true;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var achievement = await dbContext.Achievements
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (achievement is null)
            return false;

        var hasEarnedUsers = await dbContext.UserAchievements
            .AnyAsync(ua => ua.AchievementId == id, cancellationToken);

        if (hasEarnedUsers)
            throw new InvalidOperationException(
                "Cannot delete an achievement that has already been awarded.");

        dbContext.Achievements.Remove(achievement);
        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Achievements);

        return true;
    }

    public async Task<IReadOnlyList<UserAchievement>> GetUserAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserAchievements
            .AsNoTracking()
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.EarnedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AwardAsync(
        Guid userId,
        string achievementCode,
        BalanceType balanceType,
        TransactionType transactionType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user ID is required.", nameof(userId));

        achievementCode = achievementCode.Trim().ToUpperInvariant();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var userExists = await dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
            throw new KeyNotFoundException("User was not found.");

        var achievement = await dbContext.Achievements
            .FirstOrDefaultAsync(
                a => a.Code == achievementCode && a.IsActive,
                cancellationToken);

        if (achievement is null)
            throw new KeyNotFoundException(
                $"Active achievement '{achievementCode}' was not found.");

        if (achievement.RewardAmount < 0)
            throw new InvalidOperationException(
                "Achievement reward cannot be negative.");

        var alreadyEarned = await dbContext.UserAchievements
            .AnyAsync(
                ua => ua.UserId == userId &&
                      ua.AchievementId == achievement.Id,
                cancellationToken);

        if (alreadyEarned)
        {
            await transaction.RollbackQuietlyAsync();
            return false;
        }

        var balance = await dbContext.Balances
            .FirstOrDefaultAsync(
                b => b.UserId == userId && b.Type == balanceType,
                cancellationToken);

        var isNewBalance = balance is null;

        balance ??= new Balance
        {
            UserId = userId,
            Type = balanceType,
            Amount = 0,
            Version = 0,
            UpdatedAt = DateTime.UtcNow
        };

        long newAmount;

        try
        {
            newAmount = checked(balance.Amount + achievement.RewardAmount);

            if (achievement.RewardAmount > 0)
                balance.Version = checked(balance.Version + 1);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                "The reward exceeds the supported balance range.");
        }

        balance.Amount = newAmount;
        balance.UpdatedAt = DateTime.UtcNow;

        if (isNewBalance)
            dbContext.Balances.Add(balance);

        dbContext.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            AchievementId = achievement.Id,
            EarnedAt = DateTime.UtcNow
        });

        if (achievement.RewardAmount > 0)
        {
            dbContext.Transactions.Add(new Transaction
            {
                UserId = userId,
                Type = transactionType,
                Amount = achievement.RewardAmount,
                BalanceAfter = newAmount,
                Description = $"Получено достижение \"{achievement.Name}\"",
                RelatedEntityType = "achievement",
                RelatedEntityId = achievement.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}