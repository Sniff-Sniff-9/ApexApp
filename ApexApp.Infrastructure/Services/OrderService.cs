using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace ApexApp.Infrastructure.Services;

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IAchievementService _achievementService;
    private readonly ILogger<OrderService> _logger;
    private readonly ICacheService _cache;

    public OrderService(
        AppDbContext dbContext,
        IAchievementService achievementService,
        ILogger<OrderService> logger,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _achievementService = achievementService;
        _logger = logger;
        _cache = cache;
    }

    public async Task<(bool Success, int StatusCode, string? Error, Order? Data, Achievement? NewAchievement)>
        CheckoutAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return (false, 400, "Invalid user ID.", null, null);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

        try
        {
            var cartItems = await _dbContext.Set<CartItem>()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.ProductId)
                .ToListAsync(cancellationToken);

            if (cartItems.Count == 0)
                return (false, 400, "Cart is empty.", null, null);

            if (cartItems.Any(x => x.Quantity <= 0))
                return (false, 400, "Cart contains an invalid quantity.", null, null);

            var productIds = cartItems.Select(x => x.ProductId).Distinct().ToArray();

            var products = await _dbContext.Set<Product>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.products
                    WHERE id = ANY ({productIds})
                    ORDER BY id
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken);

            if (products.Count != productIds.Length)
                return (false, 409, "One or more products are unavailable.", null, null);

            var productsById = products.ToDictionary(p => p.Id);

            // Варианты блокируются отдельным запросом: остаток варианта уменьшается
            // в той же транзакции, поэтому конкурентная покупка не может его превысить.
            var variantIds = cartItems
                .Where(item => item.ProductVariantId.HasValue)
                .Select(item => item.ProductVariantId!.Value)
                .Distinct()
                .ToArray();

            var variants = variantIds.Length == 0
                ? []
                : await _dbContext.Set<ProductVariant>()
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM public.product_variants
                        WHERE id = ANY ({variantIds})
                        ORDER BY id
                        FOR UPDATE
                        """)
                    .ToListAsync(cancellationToken);

            if (variants.Count != variantIds.Length)
                return (false, 409, "One or more product variants are unavailable.", null, null);

            var variantsById = variants.ToDictionary(variant => variant.Id);

            long total = 0;

            foreach (var item in cartItems)
            {
                var product = productsById[item.ProductId];
                var variant = item.ProductVariantId is int selectedVariantId
                    ? variantsById.GetValueOrDefault(selectedVariantId)
                    : null;

                if (item.ProductVariantId.HasValue && variant is null)
                    return (false, 409, "One or more product variants are unavailable.", null, null);

                var selection = ProductVariantPricing.Evaluate(product, variant, item.Quantity);

                switch (selection.Status)
                {
                    case ProductSelectionStatus.Ok:
                        break;

                    case ProductSelectionStatus.ProductUnavailable:
                        return (false, 409, $"Product '{product.Name}' is unavailable.", null, null);

                    case ProductSelectionStatus.VariantUnavailable:
                        return (false, 409, $"Variant of '{product.Name}' is unavailable.", null, null);

                    case ProductSelectionStatus.InsufficientStock:
                        return (false, 409, DescribeStockShortage(product, variant), null, null);

                    default:
                        return (false, 400, "Cart contains an invalid quantity.", null, null);
                }

                try
                {
                    total = checked(total + checked(selection.UnitPrice * item.Quantity));
                }
                catch (OverflowException)
                {
                    return (false, 400, "Order total exceeds the supported range.", null, null);
                }
            }

            var balance = await _dbContext.Balances
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.balances
                    WHERE user_id = {userId}
                      AND type = 'personal'::balance_type
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (balance is null || balance.Amount < total)
                return (false, 409, "Insufficient personal balance.", null, null);

            long newBalance;

            try
            {
                newBalance = checked(balance.Amount - total);
                balance.Version = checked(balance.Version + 1);
            }
            catch (OverflowException)
            {
                return (false, 400, "Balance exceeds the supported range.", null, null);
            }

            var now = DateTime.UtcNow;

            var hasPreviousPaidOrder = await _dbContext.Set<Order>()
            .AnyAsync(
                o => o.UserId == userId &&
                     o.Status == OrderStatus.Paid,
                cancellationToken);

            var isFirstOrder = !hasPreviousPaidOrder;

            var order = new Order
            {
                UserId = userId,
                Status = OrderStatus.Paid,
                TotalAmount = total,
                CreatedAt = now,
                UpdatedAt = now,
                OrderItems = cartItems.Select(item =>
                {
                    var product = productsById[item.ProductId];
                    var variant = item.ProductVariantId is int selectedVariantId
                        ? variantsById.GetValueOrDefault(selectedVariantId)
                        : null;

                    return new OrderItem
                    {
                        ProductId = product.Id,
                        Product = product,
                        ProductVariantId = variant?.Id,
                        ProductVariant = variant,
                        Quantity = item.Quantity,
                        PriceAtPurchase = ProductVariantPricing.ResolveUnitPrice(product, variant)
                    };
                }).ToList()
            };

            balance.Amount = newBalance;
            balance.UpdatedAt = now;

            foreach (var item in cartItems)
            {
                var product = productsById[item.ProductId];

                if (item.ProductVariantId is int selectedVariantId
                    && variantsById.TryGetValue(selectedVariantId, out var variant))
                {
                    // Остаток проверен выше в этой же транзакции, поэтому вычитание безопасно.
                    variant.Stock -= item.Quantity;
                }
                else if (product.Stock.HasValue)
                {
                    product.Stock -= item.Quantity;
                }

                product.UpdatedAt = now;
            }

            _dbContext.Set<Order>().Add(order);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.Set<Transaction>().Add(new Transaction
            {
                UserId = userId,
                Type = TransactionType.Purchase,
                Amount = -total,
                BalanceAfter = newBalance,
                Description = "Заказ от " + now.ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = userId,
                RelatedEntityType = "order",
                RelatedEntityId = order.Id,
                CreatedAt = now
            });

            _dbContext.Set<CartItem>().RemoveRange(cartItems);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Checkout decrements product stock, which is part of the cached catalog.
            _cache.RemoveByPrefix(CacheKeys.ProductsPrefix);

            Achievement? newAchievement = null;

            if (isFirstOrder)
            {
                try
                {
                    var achievementAwarded = await _achievementService.AwardAsync(
                        userId,
                        "FIRST_ORDER",
                        BalanceType.Personal,
                        TransactionType.AchievementReward,
                        cancellationToken);

                    if (achievementAwarded)
                    {
                        newAchievement = await _dbContext.Achievements
                            .AsNoTracking()
                            .FirstOrDefaultAsync(
                                a => a.Code == "FIRST_ORDER",
                                cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to award FIRST_ORDER for user {UserId}, order {OrderId}",
                        userId,
                        order.Id);
                }
            }

            return (true, 201, null, order, newAchievement);

        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.SerializationFailure ||
                  ex.SqlState == PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(cancellationToken);

            return (false, 409,
                "A concurrent purchase changed the data. Please retry.", null, null);
        }
    }

    public Task<List<Order>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Order>()
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdAsync(
        Guid userId,
        long orderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Order>()
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.Id == orderId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Order>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Order>()
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
            .ToListAsync(cancellationToken);     
    }

    public async Task<(
    bool Success,
    int StatusCode,
    string? Error,
    Order? Data)> UpdateStatusAsync(
        long orderId,
        Guid employeeId,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || employeeId == Guid.Empty)
            return (false, 400, "Invalid order or employee ID.", null);

        if (newStatus is not (
            OrderStatus.Paid or
            OrderStatus.Processing or
            OrderStatus.Ready or
            OrderStatus.Delivered))
        {
            return (false, 400, "Unsupported target status.", null);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var order = await _dbContext.Set<Order>()
            .FirstOrDefaultAsync(
                o => o.Id == orderId,
                cancellationToken);

        if (order is null)
        {
            return (false, 404, "Order was not found.", null);
        }

        var validTransition = (order.Status, newStatus) switch
        {
            (OrderStatus.Paid, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Ready) => true,
            (OrderStatus.Ready, OrderStatus.Delivered) => true,
            _ => false
        };

        if (!validTransition)
        {
            return (
                false,
                409,
                $"Cannot change order status from {order.Status} to {newStatus}.",
                null);
        }

        var now = DateTime.UtcNow;

        order.Status = newStatus;
        order.UpdatedAt = now;

        if (newStatus == OrderStatus.Processing)
            order.ProcessedBy = employeeId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, 200, null, order);
    }

    public async Task<(bool Success, int StatusCode, string? Error, Order? Data)>
    CancelAsync(
        long orderId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || employeeId == Guid.Empty)
            return (false, 400, "Invalid order or employee ID.", null);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var order = await _dbContext.Set<Order>()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ProductVariant)
                .FirstOrDefaultAsync(
                    o => o.Id == orderId,
                    cancellationToken);

            if (order is null)
                return (false, 404, "Order was not found.", null);

            // Отменяем только оплаченные заказы, которые ещё не начали обрабатывать.
            if (order.Status != OrderStatus.Paid)
            {
                return (
                    false,
                    409,
                    $"Cannot cancel order with status {order.Status}. Only paid orders can be cancelled.",
                    null);
            }

            var balance = await _dbContext.Balances
                .FromSqlInterpolated($"""
                SELECT *
                FROM public.balances
                WHERE user_id = {order.UserId}
                  AND type = 'personal'::balance_type
                FOR UPDATE
                """)
                .SingleOrDefaultAsync(cancellationToken);

            if (balance is null)
                return (false, 409, "Personal balance was not found.", null);

            long newBalance;

            try
            {
                newBalance = checked(balance.Amount + order.TotalAmount);
                balance.Version = checked(balance.Version + 1);
            }
            catch (OverflowException)
            {
                return (false, 400, "Balance exceeds the supported range.", null);
            }

            var now = DateTime.UtcNow;

            balance.Amount = newBalance;
            balance.UpdatedAt = now;

            foreach (var item in order.OrderItems)
            {
                // Остаток возвращается туда же, откуда он был списан: вариант или товар.
                if (item.ProductVariant is not null)
                {
                    try
                    {
                        item.ProductVariant.Stock = checked(
                            item.ProductVariant.Stock + item.Quantity);
                    }
                    catch (OverflowException)
                    {
                        return (
                            false,
                            400,
                            $"Stock exceeds the supported range for variant '{item.ProductVariant.Name}'.",
                            null);
                    }

                    if (item.Product is not null)
                    {
                        item.Product.UpdatedAt = now;
                    }

                    continue;
                }

                if (item.Product is null)
                    continue;

                if (item.Product.Stock.HasValue)
                {
                    try
                    {
                        item.Product.Stock = checked(
                            item.Product.Stock.Value + item.Quantity);
                    }
                    catch (OverflowException)
                    {
                        return (
                            false,
                            400,
                            $"Stock exceeds the supported range for '{item.Product.Name}'.",
                            null);
                    }
                }

                item.Product.UpdatedAt = now;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = now;

            _dbContext.Set<Transaction>().Add(new Transaction
            {
                UserId = order.UserId,
                Type = TransactionType.Refund,
                Amount = order.TotalAmount,
                BalanceAfter = newBalance,
                Description = $"Возврат средств за отмененный заказ от {order.CreatedAt}",
                CreatedBy = employeeId,
                RelatedEntityType = "order",
                RelatedEntityId = order.Id,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, 200, null, order);
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.SerializationFailure ||
                  ex.SqlState == PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(cancellationToken);

            return (
                false,
                409,
                "A concurrent operation changed the data. Please retry.",
                null);
        }
    }

    /// <summary>
    /// Возвращает сообщение о нехватке остатка с указанием выбранного варианта.
    /// </summary>
    private static string DescribeStockShortage(Product product, ProductVariant? variant) =>
        variant is null
            ? $"Insufficient stock for '{product.Name}'."
            : $"Insufficient stock for '{product.Name} / {variant.Name}'.";
}