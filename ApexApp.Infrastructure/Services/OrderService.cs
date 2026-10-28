using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            // Признак первого заказа читается тем же запросом, что и корзина: блок выполняется
            // в транзакции Serializable, поэтому оба значения относятся к одному снимку данных.
            var cartRows = await _dbContext.Set<CartItem>()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.ProductId)
                .Select(x => new
                {
                    Item = x,
                    HasPreviousPaidOrder = _dbContext.Set<Order>()
                        .Any(order => order.UserId == userId && order.Status == OrderStatus.Paid)
                })
                .ToListAsync(cancellationToken);

            if (cartRows.Count == 0)
                return (false, 400, "Cart is empty.", null, null);

            var cartItems = cartRows.Select(row => row.Item).ToList();

            if (cartItems.Any(x => x.Quantity <= 0))
                return (false, 400, "Cart contains an invalid quantity.", null, null);

            var isFirstOrder = !cartRows[0].HasPreviousPaidOrder;

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

            long total = 0;

            foreach (var item in cartItems)
            {
                var product = productsById[item.ProductId];

                if (!product.IsActive || product.IsDigital)
                    return (false, 409, $"Product '{product.Name}' is unavailable.", null, null);

                if (product.Stock.HasValue && product.Stock.Value < item.Quantity)
                    return (false, 409, $"Insufficient stock for '{product.Name}'.", null, null);

                try
                {
                    total = checked(total + checked(product.Price * item.Quantity));
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

                    return new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        PriceAtPurchase = product.Price
                    };
                }).ToList()
            };

            balance.Amount = newBalance;
            balance.UpdatedAt = now;

            foreach (var item in cartItems)
            {
                var product = productsById[item.ProductId];

                if (product.Stock.HasValue)
                    product.Stock -= item.Quantity;

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

            // Позиции корзины удаляются одним DELETE по прочитанным выше идентификаторам:
            // загружать сущности в трекер только ради удаления не нужно.
            var cartItemIds = cartItems.Select(item => item.Id).ToArray();

            await _dbContext.Set<CartItem>()
                .Where(item => cartItemIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);

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
                        // Справочник ачивок кэшируется, поэтому описание берётся из кэша
                        // без дополнительного запроса после SaveChanges.
                        newAchievement = await FindAchievementAsync(
                            "FIRST_ORDER", cancellationToken);
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
        catch (Exception ex) when (DatabaseErrors.IsRetryableConflict(ex))
        {
            // DatabaseErrors разворачивает DbUpdateException, которым EF оборачивает
            // ошибки 40001/40P01: раньше такие конфликты уходили клиенту как 500.
            await transaction.RollbackQuietlyAsync();

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
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Order>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Order>()
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
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

        // Целевой статус однозначно определяет ожидаемый текущий статус, поэтому
        // переход выполняется одним условным UPDATE. Отдельных BEGIN/COMMIT и
        // чтения перед записью больше нет: повторный или конкурентный запрос
        // получает 0 изменённых строк вместо ошибки сериализации (40001).
        OrderStatus? expectedStatus = newStatus switch
        {
            OrderStatus.Processing => OrderStatus.Paid,
            OrderStatus.Ready => OrderStatus.Processing,
            OrderStatus.Delivered => OrderStatus.Ready,
            _ => null
        };

        if (expectedStatus is null)
        {
            // Переход в Paid не предусмотрен: сохраняем прежние ответы 404/409.
            var currentStatus = await _dbContext.Set<Order>()
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return currentStatus is null
                ? (false, 404, "Order was not found.", null)
                : (false, 409, $"Cannot change order status from {currentStatus} to {newStatus}.", null);
        }

        var now = DateTime.UtcNow;

        var query = _dbContext.Set<Order>()
            .Where(o => o.Id == orderId && o.Status == expectedStatus.Value);

        var updated = newStatus == OrderStatus.Processing
            ? await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, newStatus)
                    .SetProperty(o => o.UpdatedAt, now)
                    .SetProperty(o => o.ProcessedBy, employeeId),
                cancellationToken)
            : await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, newStatus)
                    .SetProperty(o => o.UpdatedAt, now),
                cancellationToken);

        if (updated == 0)
        {
            var currentStatus = await _dbContext.Set<Order>()
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => (OrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return currentStatus is null
                ? (false, 404, "Order was not found.", null)
                : (false, 409, $"Cannot change order status from {currentStatus} to {newStatus}.", null);
        }

        var order = await _dbContext.Set<Order>()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null
            ? (false, 404, "Order was not found.", null)
            : (true, 200, null, order);
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

            // Отмена возвращает товар на склад, поэтому строки products блокируются
            // до чтения остатка и в том же порядке, что и при оформлении заказа
            // (ORDER BY id). Раньше остаток читался без блокировки и записывался
            // после блокировки баланса: порядок блокировок не совпадал с checkout
            // (риск взаимоблокировки), а запись опиралась на незаблокированное чтение.
            var productIds = order.OrderItems
                .Where(item => item.ProductId.HasValue)
                .Select(item => item.ProductId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            if (productIds.Length > 0)
            {
                await _dbContext.Set<Product>()
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM public.products
                        WHERE id = ANY ({productIds})
                        ORDER BY id
                        FOR UPDATE
                        """)
                    .ToListAsync(cancellationToken);
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

            // Восстановленный остаток входит в кэшированный каталог: без сброса
            // кэша витрина показывала бы старое количество до истечения TTL.
            _cache.RemoveByPrefix(CacheKeys.ProductsPrefix);

            return (true, 200, null, order);
        }
        catch (Exception ex) when (DatabaseErrors.IsRetryableConflict(ex))
        {
            await transaction.RollbackQuietlyAsync();

            return (
                false,
                409,
                "A concurrent operation changed the data. Please retry.",
                null);
        }
    }

    /// <summary>
    /// Возвращает описание ачивки по коду из кэшированного справочника,
    /// чтобы не выполнять отдельный запрос после выдачи награды.
    /// </summary>
    /// <param name="code">Код ачивки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task<Achievement?> FindAchievementAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var achievements = await _achievementService.GetAllAsync(cancellationToken);

        return achievements.FirstOrDefault(a => a.Code == code);
    }
}