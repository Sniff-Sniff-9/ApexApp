using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class ThankService(AppDbContext dbContext) : IThankService
{
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

        return ThanksResult.Ok(thank);
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
}

public sealed record ThanksResult(
    int StatusCode,
    string? Error,
    Thank? Data)
{
    public static ThanksResult Fail(int status, string error) =>
        new(status, error, null);

    public static ThanksResult Ok(Thank thank) =>
        new(201, null, thank);
}

public sealed record CreateThankRequest(
    Guid ToUserId,
    long Amount,
    string? Message,
    bool IsPublic);