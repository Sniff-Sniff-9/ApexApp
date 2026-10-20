using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApexApp.Infrastructure.Services;

public sealed class OrderService(AppDbContext dbContext) : IOrderService
{
    public async Task<(bool Success, int StatusCode, string? Error, Order? Data)>
        CheckoutAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return (false, 400, "Invalid user ID.", null);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

        try
        {
            var cartItems = await dbContext.Set<CartItem>()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.ProductId)
                .ToListAsync(cancellationToken);

            if (cartItems.Count == 0)
                return (false, 400, "Cart is empty.", null);

            if (cartItems.Any(x => x.Quantity <= 0))
                return (false, 400, "Cart contains an invalid quantity.", null);

            var productIds = cartItems.Select(x => x.ProductId).Distinct().ToArray();

            var products = await dbContext.Set<Product>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.products
                    WHERE id = ANY ({productIds})
                    ORDER BY id
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken);

            if (products.Count != productIds.Length)
                return (false, 409, "One or more products are unavailable.", null);

            var productsById = products.ToDictionary(p => p.Id);

            long total = 0;

            foreach (var item in cartItems)
            {
                var product = productsById[item.ProductId];

                if (!product.IsActive || product.IsDigital)
                    return (false, 409, $"Product '{product.Name}' is unavailable.", null);

                if (product.Stock.HasValue && product.Stock.Value < item.Quantity)
                    return (false, 409, $"Insufficient stock for '{product.Name}'.", null);

                try
                {
                    total = checked(total + checked(product.Price * item.Quantity));
                }
                catch (OverflowException)
                {
                    return (false, 400, "Order total exceeds the supported range.", null);
                }
            }

            var balance = await dbContext.Balances
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.balances
                    WHERE user_id = {userId}
                      AND type = 'personal'::balance_type
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (balance is null || balance.Amount < total)
                return (false, 409, "Insufficient personal balance.", null);

            long newBalance;

            try
            {
                newBalance = checked(balance.Amount - total);
                balance.Version = checked(balance.Version + 1);
            }
            catch (OverflowException)
            {
                return (false, 400, "Balance exceeds the supported range.", null);
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

            dbContext.Set<Order>().Add(order);

            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.Set<Transaction>().Add(new Transaction
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

            dbContext.Set<CartItem>().RemoveRange(cartItems);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, 201, null, order);
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.SerializationFailure ||
                  ex.SqlState == PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(cancellationToken);

            return (false, 409,
                "A concurrent purchase changed the data. Please retry.", null);
        }
    }

    public Task<List<Order>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Order>()
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
        return dbContext.Set<Order>()
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.Id == orderId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(cancellationToken);
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
            OrderStatus.Processing or
            OrderStatus.Ready or
            OrderStatus.Delivered))
        {
            return (false, 400, "Unsupported target status.", null);
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var order = await dbContext.Set<Order>()
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

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, 200, null, order);
    }
}