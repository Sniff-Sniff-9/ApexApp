using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ApexApp.Infrastructure.Services;

public sealed class ThankService : IThankService
{
    private readonly AppDbContext dbContext;
    private readonly IAchievementService _achievementService;
    private readonly ILogger<ThankService> _logger;
    private readonly ICacheService _cache;

    public ThankService(
        AppDbContext dbContext,
        IAchievementService achievementService,
        ILogger<ThankService> logger,
        ICacheService cache)
    {
        this.dbContext = dbContext;
        _achievementService = achievementService;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ThanksResult> CreateAsync(
        Guid fromUserId,
        CreateThankRequest request,
        CancellationToken cancellationToken = default)
    {
        if (fromUserId == Guid.Empty ||
            request.ToUserId == Guid.Empty)
            return ThanksResult.Fail(400, "Invalid user ID.");

        if (fromUserId == request.ToUserId)
            return ThanksResult.Fail(400, "You cannot thank yourself.");

        if (request.Amount <= 0)
            return ThanksResult.Fail(400, "Amount must be positive.");

        var now = DateTime.UtcNow;

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

        var senderExists = await dbContext.Users
            .AnyAsync(u => u.Id == fromUserId, cancellationToken);

        if (!senderExists)
            return ThanksResult.Fail(404, "Sender was not found.");

        var recipientExists = await dbContext.Users
            .AnyAsync(u => u.Id == request.ToUserId, cancellationToken);

        if (!recipientExists)
            return ThanksResult.Fail(404, "Recipient was not found.");

        var alreadyThanked = await dbContext.Set<Thank>()
            .AnyAsync(t =>
                t.FromUserId == fromUserId &&
                t.ToUserId == request.ToUserId &&
                t.CreatedAt > now.AddDays(-7),
                cancellationToken);

        if (alreadyThanked)
            return ThanksResult.Fail(
                409, "You have already thanked this user in the last 7 days.");

        var hasPreviousThank = await dbContext.Set<Thank>()
            .AnyAsync(
                t => t.FromUserId == fromUserId,
                cancellationToken);

        var isFirstThank = !hasPreviousThank;

        var balance = await dbContext.Balances
            .SingleOrDefaultAsync(
                b => b.UserId == request.ToUserId &&
                     b.Type == BalanceType.Personal,
                cancellationToken);

        var isNewBalance = balance is null;

        balance ??= new Balance
        {
            UserId = request.ToUserId,
            Type = BalanceType.Personal,
            Amount = 0,
            Version = 0,
            UpdatedAt = now
        };

        long newAmount;

        try
        {
            newAmount = checked(balance.Amount + request.Amount);
            balance.Version = checked(balance.Version + 1);
        }
        catch (OverflowException)
        {
            return ThanksResult.Fail(400, "Balance exceeds the supported range.");
        }

        balance.Amount = newAmount;
        balance.UpdatedAt = now;

        if (isNewBalance)
            dbContext.Balances.Add(balance);

        var thank = new Thank
        {
            FromUserId = fromUserId,
            ToUserId = request.ToUserId,
            Amount = request.Amount,
            Message = request.Message,
            IsPublic = request.IsPublic,
            CreatedAt = now
        };

        dbContext.Set<Thank>().Add(thank);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Set<Transaction>().Add(new Transaction
        {
            UserId = request.ToUserId,
            Type = TransactionType.Thanks,
            Amount = request.Amount,
            BalanceAfter = newAmount,
            Description = request.Message,
            CreatedBy = fromUserId,
            RelatedUserId = fromUserId,
            RelatedEntityType = "thank",
            RelatedEntityId = thank.Id,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        // The leaderboard aggregates the number of thanks received.
        _cache.RemoveByPrefix(CacheKeys.LeaderboardPrefix);

        Achievement? newAchievement = null;

        if (isFirstThank)
        {
            try
            {
                var achievementAwarded = await _achievementService.AwardAsync(
                    fromUserId,
                    "FIRST_THANK",
                    BalanceType.Personal,
                    TransactionType.AchievementReward,
                    cancellationToken);

                if (achievementAwarded)
                {
                    newAchievement = await dbContext.Achievements
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            a => a.Code == "FIRST_THANK",
                            cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to award FIRST_THANK for user {UserId}, thank {ThankId}",
                    fromUserId,
                    thank.Id);
            }
        }

        return ThanksResult.Ok(thank, newAchievement);
    }

    public async Task<List<Thank>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Thank>()
            .AsNoTracking().Where(t => t.ToUserId == userId )
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Thank>> GetAllFromMeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Thank>()
            .AsNoTracking().Where(t => t.FromUserId == userId )
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Thank?> GetByIdAsync(
        Guid userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Thank>()
            .AsNoTracking().Where(t => t.ToUserId == userId)
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ThanksResult> GiveManagerBonusAsync(
    Guid managerId,
    CreateManagerBonusRequest request,
    CancellationToken cancellationToken = default)
    {
        if (managerId == Guid.Empty || request.ToUserId == Guid.Empty)
            return ThanksResult.Fail(400, "Invalid user ID.");

        if (managerId == request.ToUserId)
            return ThanksResult.Fail(400, "You cannot award yourself.");

        if (request.Amount <= 0)
            return ThanksResult.Fail(400, "Amount must be positive.");

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var now = DateTime.UtcNow;

        var managerExists = await dbContext.Users
            .AnyAsync(u => u.Id == managerId, cancellationToken);

        if (!managerExists)
            return ThanksResult.Fail(404, "Manager was not found.");

        var recipientExists = await dbContext.Users
            .AnyAsync(u => u.Id == request.ToUserId, cancellationToken);

        if (!recipientExists)
            return ThanksResult.Fail(404, "Recipient was not found.");

        var managerBalance = await dbContext.Balances
            .SingleOrDefaultAsync(
                b => b.UserId == managerId &&
                     b.Type == BalanceType.Reward,
                cancellationToken);

        if (managerBalance is null ||
            managerBalance.Amount < request.Amount)
        {
            return ThanksResult.Fail(
                409, "Insufficient reward balance.");
        }

        var recipientBalance = await dbContext.Balances
            .SingleOrDefaultAsync(
                b => b.UserId == request.ToUserId &&
                     b.Type == BalanceType.Personal,
                cancellationToken);

        var isNewRecipientBalance = recipientBalance is null;

        recipientBalance ??= new Balance
        {
            UserId = request.ToUserId,
            Type = BalanceType.Personal,
            Amount = 0,
            Version = 0,
            UpdatedAt = now
        };

        long managerNewAmount;
        long recipientNewAmount;

        try
        {
            managerNewAmount =
                checked(managerBalance.Amount - request.Amount);

            recipientNewAmount =
                checked(recipientBalance.Amount + request.Amount);

            managerBalance.Version =
                checked(managerBalance.Version + 1);

            recipientBalance.Version =
                checked(recipientBalance.Version + 1);
        }
        catch (OverflowException)
        {
            return ThanksResult.Fail(
                400, "Balance exceeds the supported range.");
        }

        managerBalance.Amount = managerNewAmount;
        managerBalance.UpdatedAt = now;

        recipientBalance.Amount = recipientNewAmount;
        recipientBalance.UpdatedAt = now;

        if (isNewRecipientBalance)
            dbContext.Balances.Add(recipientBalance);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Set<Transaction>().Add(new Transaction
        {
            UserId = managerId,
            Type = TransactionType.ManagerBonus,
            Amount = -request.Amount,
            BalanceAfter = managerNewAmount,
            Description = request.Message,
            CreatedBy = managerId,
            RelatedUserId = request.ToUserId,
            RelatedEntityType = "manager_bonus",
            CreatedAt = now
        });

        dbContext.Set<Transaction>().Add(new Transaction
        {
            UserId = request.ToUserId,
            Type = TransactionType.ManagerBonus,
            Amount = request.Amount,
            BalanceAfter = recipientNewAmount,
            Description = request.Message,
            CreatedBy = managerId,
            RelatedUserId = managerId,
            RelatedEntityType = "manager_bonus",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return new ThanksResult(201, null, null, null);
    }
}

public sealed record ThanksResult(
    int StatusCode,
    string? Error,
    Thank? Data, Achievement? NewAchievement)
{
    public static ThanksResult Fail(int status, string error) =>
        new(status, error, null, null);

    public static ThanksResult Ok(Thank thank, Achievement? newAchievement = null) =>
        new(201, null, thank, newAchievement);
}

public sealed record CreateThankRequest(
    Guid ToUserId,
    long Amount,
    string? Message,
    bool IsPublic);