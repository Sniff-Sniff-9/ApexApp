using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface IAchievementService
{
    Task<IReadOnlyList<Achievement>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Achievement?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Achievement> CreateAsync(
        Achievement achievement,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        int id, string code, string name, string? description, long rewardAmount, bool isActive,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAchievement>> GetUserAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> AwardAsync(
        Guid userId,
        string achievementCode,
        BalanceType balanceType,
        TransactionType transactionType,
        CancellationToken cancellationToken = default);
}