using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
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

        // Один запрос отдаёт всё, что нужно решению: существование отправителя и получателя,
        // признаки «уже благодарил» и «первая благодарность», а также баланс получателя.
        var sevenDaysAgo = now.AddDays(-7);

        var state = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == fromUserId)
            .Select(u => new
            {
                RecipientExists = dbContext.Users.Any(recipient => recipient.Id == request.ToUserId),
                HasRecentThank = dbContext.Set<Thank>().Any(t =>
                    t.FromUserId == fromUserId &&
                    t.ToUserId == request.ToUserId &&
                    t.CreatedAt > sevenDaysAgo),
                HasAnyThank = dbContext.Set<Thank>().Any(t => t.FromUserId == fromUserId),
                Balance = dbContext.Balances
                    .Where(b => b.UserId == request.ToUserId && b.Type == BalanceType.Personal)
                    .Select(b => new { b.Amount, b.Version })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (state is null)
            return ThanksResult.Fail(404, "Sender was not found.");

        if (!state.RecipientExists)
            return ThanksResult.Fail(404, "Recipient was not found.");

        if (state.HasRecentThank)
            return ThanksResult.Fail(
                409, "You have already thanked this user in the last 7 days.");

        var isFirstThank = !state.HasAnyThank;

        var currentAmount = state.Balance?.Amount ?? 0;
        var currentVersion = state.Balance?.Version ?? 0;

        long newAmount;
        int newVersion;

        try
        {
            newAmount = checked(currentAmount + request.Amount);
            newVersion = checked(currentVersion + 1);
        }
        catch (OverflowException)
        {
            return ThanksResult.Fail(400, "Balance exceeds the supported range.");
        }

        if (state.Balance is null)
        {
            dbContext.Balances.Add(new Balance
            {
                UserId = request.ToUserId,
                Type = BalanceType.Personal,
                Amount = newAmount,
                Version = newVersion,
                UpdatedAt = now
            });
        }
        else
        {
            // Баланс записывается одним UPDATE: сущность не попадает в трекер,
            // а строка блокируется транзакцией на время изменения.
            await dbContext.Balances
                .Where(b => b.UserId == request.ToUserId &&
                            b.Type == BalanceType.Personal)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(b => b.Amount, newAmount)
                        .SetProperty(b => b.Version, newVersion)
                        .SetProperty(b => b.UpdatedAt, now),
                    cancellationToken);
        }

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

        try
        {
            // thank.Id нужен для RelatedEntityId, поэтому сохранение двухэтапное.
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
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            // Ранее ошибки сериализации (40001) и взаимоблокировки (40P01)
            // уходили клиенту как 500 вместо корректного 409.
            return ThanksResult.Fail(
                409, "A concurrent thank changed the data. Please retry.");
        }

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
                    // Справочник ачивок кэширован, поэтому описание берётся
                    // без дополнительного запроса после SaveChanges.
                    var achievements = await _achievementService
                        .GetAllAsync(cancellationToken);

                    newAchievement = achievements
                        .FirstOrDefault(a => a.Code == "FIRST_THANK");
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

        // Один запрос отдаёт существование получателя и оба баланса:
        // отдельные чтения пользователей и балансов больше не нужны.
        var state = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == managerId)
            .Select(u => new
            {
                RecipientExists = dbContext.Users.Any(recipient => recipient.Id == request.ToUserId),
                ManagerBalance = dbContext.Balances
                    .Where(b => b.UserId == managerId && b.Type == BalanceType.Reward)
                    .Select(b => new { b.Amount, b.Version })
                    .FirstOrDefault(),
                RecipientBalance = dbContext.Balances
                    .Where(b => b.UserId == request.ToUserId && b.Type == BalanceType.Personal)
                    .Select(b => new { b.Amount, b.Version })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (state is null)
            return ThanksResult.Fail(404, "Manager was not found.");

        if (!state.RecipientExists)
            return ThanksResult.Fail(404, "Recipient was not found.");

        if (state.ManagerBalance is null ||
            state.ManagerBalance.Amount < request.Amount)
        {
            return ThanksResult.Fail(
                409, "Insufficient reward balance.");
        }

        var managerAmount = state.ManagerBalance.Amount;
        var managerVersion = state.ManagerBalance.Version;
        var isNewRecipientBalance = state.RecipientBalance is null;
        var recipientAmount = state.RecipientBalance?.Amount ?? 0;
        var recipientVersion = state.RecipientBalance?.Version ?? 0;

        long managerNewAmount;
        long recipientNewAmount;
        int managerNewVersion;
        int recipientNewVersion;

        try
        {
            managerNewAmount = checked(managerAmount - request.Amount);
            recipientNewAmount = checked(recipientAmount + request.Amount);
            managerNewVersion = checked(managerVersion + 1);
            recipientNewVersion = checked(recipientVersion + 1);
        }
        catch (OverflowException)
        {
            return ThanksResult.Fail(
                400, "Balance exceeds the supported range.");
        }

        // Балансы записываются двумя UPDATE в прежнем порядке (менеджер, затем получатель),
        // поэтому порядок блокировки строк не меняется.
        await dbContext.Balances
            .Where(b => b.UserId == managerId && b.Type == BalanceType.Reward)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(b => b.Amount, managerNewAmount)
                    .SetProperty(b => b.Version, managerNewVersion)
                    .SetProperty(b => b.UpdatedAt, now),
                cancellationToken);

        if (isNewRecipientBalance)
        {
            dbContext.Balances.Add(new Balance
            {
                UserId = request.ToUserId,
                Type = BalanceType.Personal,
                Amount = recipientNewAmount,
                Version = recipientNewVersion,
                UpdatedAt = now
            });
        }
        else
        {
            await dbContext.Balances
                .Where(b => b.UserId == request.ToUserId && b.Type == BalanceType.Personal)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(b => b.Amount, recipientNewAmount)
                        .SetProperty(b => b.Version, recipientNewVersion)
                        .SetProperty(b => b.UpdatedAt, now),
                    cancellationToken);
        }

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

        try
        {
            // Балансы и обе записи ledger сохраняются одним SaveChanges:
            // зависимостей по сгенерированным идентификаторам между ними нет.
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            return ThanksResult.Fail(
                409, "A concurrent bonus changed the data. Please retry.");
        }

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