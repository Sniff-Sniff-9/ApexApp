using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexApp.Checks;

/// <summary>
/// End-to-end checks of the checkout flow against the real database: a purchase with a
/// variant and a plain purchase without a variant.
///
/// The checks write to the database, so they run only when the caller passes
/// <c>--checkout-write</c>. They create dedicated products, perform real cart →
/// checkout → cancel cycles, and remove every row they created in a <c>finally</c>
/// block (including restoring the balance snapshot).
/// </summary>
internal static class CheckoutChecks
{
    public static async Task RunAsync(CheckRunner runner, ServiceProvider provider)
    {
        await runner.CheckAsync("checkout.variant-price-stock-and-history", async () =>
        {
            await using var scope = provider.CreateAsyncScope();

            await RunScenarioAsync(
                scope.ServiceProvider,
                (dbContext, cartService, orderService, userId, productIds) => RunVariantScenarioAsync(
                    dbContext,
                    cartService,
                    orderService,
                    userId,
                    productIds));
        });

        await runner.CheckAsync("checkout.plain-product-regression", async () =>
        {
            await using var scope = provider.CreateAsyncScope();

            await RunScenarioAsync(
                scope.ServiceProvider,
                (dbContext, cartService, orderService, userId, productIds) => RunPlainScenarioAsync(
                    dbContext,
                    cartService,
                    orderService,
                    userId,
                    productIds));
        });
    }

    private static async Task RunScenarioAsync(
        IServiceProvider services,
        Func<AppDbContext, ICartService, IOrderService, Guid, List<int>, Task> scenario)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var cartService = services.GetRequiredService<ICartService>();
        var orderService = services.GetRequiredService<IOrderService>();

        var startedAt = DateTime.UtcNow;

        // Пользователь с уже оплаченным заказом: это исключает выдачу достижения
        // за первый заказ во время проверки.
        var userId = await dbContext.Orders
            .Where(order => order.Status == OrderStatus.Paid)
            .Select(order => order.UserId)
            .FirstOrDefaultAsync();

        CheckRunner.Ensure(
            userId != Guid.Empty,
            "no user with a paid order is available for the checkout check");

        var balance = await dbContext.Balances
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Type == BalanceType.Personal)
            .Select(item => new { item.Amount, item.Version })
            .FirstOrDefaultAsync();

        CheckRunner.Ensure(balance is not null, "the test user has no personal balance");

        var productsBefore = await dbContext.Products.CountAsync();
        var variantsBefore = await dbContext.ProductVariants.CountAsync();
        var ordersBefore = await dbContext.Orders.CountAsync();
        var transactionsBefore = await dbContext.Transactions.CountAsync();

        var productIds = new List<int>();

        try
        {
            await scenario(dbContext, cartService, orderService, userId, productIds);
        }
        finally
        {
            await CleanupAsync(
                dbContext,
                userId,
                productIds,
                balance!.Amount,
                balance.Version,
                startedAt);
        }

        // Проверка отсутствия следов: количество строк возвращается к исходному.
        CheckRunner.Ensure(
            await dbContext.Products.CountAsync() == productsBefore,
            "the temporary product must be removed");
        CheckRunner.Ensure(
            await dbContext.ProductVariants.CountAsync() == variantsBefore,
            "the temporary variants must be removed");
        CheckRunner.Ensure(
            await dbContext.Orders.CountAsync() == ordersBefore,
            "the temporary order must be removed");
        CheckRunner.Ensure(
            await dbContext.Transactions.CountAsync() == transactionsBefore,
            "the temporary transactions must be removed");
    }

    private static async Task RunVariantScenarioAsync(
        AppDbContext dbContext,
        ICartService cartService,
        IOrderService orderService,
        Guid userId,
        List<int> productIds)
    {
        var startedAt = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var product = new Product
        {
            Name = $"AUTOCHECK-VARIANT-{suffix}",
            Price = 10,
            Stock = 50,
            IsActive = true,
            IsDigital = false,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        productIds.Add(product.Id);

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Name = "Вариант A",
            Price = 25,
            Stock = 5,
            IsActive = true
        };

        var otherVariant = new ProductVariant
        {
            ProductId = product.Id,
            Name = "Вариант B",
            Price = 40,
            Stock = 5,
            IsActive = true
        };

        dbContext.ProductVariants.AddRange(variant, otherVariant);
        await dbContext.SaveChangesAsync();

        var added = await cartService.AddAsync(userId, product.Id, variant.Id, 2);
        CheckRunner.Ensure(added.Success, added.Error ?? "add to cart failed");

        var secondVariant = await cartService.AddAsync(userId, product.Id, otherVariant.Id, 1);
        CheckRunner.Ensure(
            !secondVariant.Success,
            "a second variant of the same product must be rejected while the cart is unique per product");

        var unknownVariant = await cartService.AddAsync(userId, product.Id, 999_999, 1);
        CheckRunner.Ensure(!unknownVariant.Success, "an unknown variant must be rejected");

        var checkout = await orderService.CheckoutAsync(userId);
        CheckRunner.Ensure(checkout.Success, checkout.Error ?? "checkout failed");

        var order = checkout.Data!;
        var item = order.OrderItems.Single();

        CheckRunner.Ensure(
            item.ProductVariantId == variant.Id,
            $"variant id must be copied to the order item, got {item.ProductVariantId}");
        CheckRunner.Ensure(
            item.PriceAtPurchase == 25,
            $"the variant price must be used, got {item.PriceAtPurchase}");
        CheckRunner.Ensure(
            order.TotalAmount == 50,
            $"order total must be 50 points, got {order.TotalAmount}");

        var history = await orderService.GetHistoryAsync(userId);
        var historyItem = history.Single(entry => entry.Id == order.Id).OrderItems.Single();
        CheckRunner.Ensure(
            historyItem.ProductVariant?.Name == "Вариант A",
            "the order history must expose the variant");

        var variantAfterCheckout = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == variant.Id);
        var productAfterCheckout = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == product.Id);

        CheckRunner.Ensure(
            variantAfterCheckout.Stock == 3,
            $"variant stock must be 3 after buying 2, got {variantAfterCheckout.Stock}");
        CheckRunner.Ensure(
            productAfterCheckout.Stock == 50,
            "product stock must not change for variant lines");

        var cancelled = await orderService.CancelAsync(order.Id, userId);
        CheckRunner.Ensure(cancelled.Success, cancelled.Error ?? "cancel failed");

        var variantAfterCancel = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == variant.Id);

        CheckRunner.Ensure(
            variantAfterCancel.Stock == 5,
            $"variant stock must be restored to 5, got {variantAfterCancel.Stock}");
    }

    private static async Task RunPlainScenarioAsync(
        AppDbContext dbContext,
        ICartService cartService,
        IOrderService orderService,
        Guid userId,
        List<int> productIds)
    {
        var startedAt = DateTime.UtcNow;

        var product = new Product
        {
            Name = $"AUTOCHECK-PLAIN-{Guid.NewGuid().ToString("N")[..8]}",
            Price = 7,
            Stock = 10,
            IsActive = true,
            IsDigital = false,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        productIds.Add(product.Id);

        // Товар без варианта: поведение оформления заказа должно остаться прежним.
        var added = await cartService.AddAsync(userId, product.Id, null, 3);
        CheckRunner.Ensure(added.Success, added.Error ?? "add to cart failed");

        var checkout = await orderService.CheckoutAsync(userId);
        CheckRunner.Ensure(checkout.Success, checkout.Error ?? "checkout failed");

        var order = checkout.Data!;
        var item = order.OrderItems.Single();

        CheckRunner.Ensure(item.ProductVariantId is null, "a plain product must not get a variant id");
        CheckRunner.Ensure(item.PriceAtPurchase == 7, $"product price expected, got {item.PriceAtPurchase}");
        CheckRunner.Ensure(order.TotalAmount == 21, $"order total must be 21, got {order.TotalAmount}");

        var productAfterCheckout = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == product.Id);

        CheckRunner.Ensure(
            productAfterCheckout.Stock == 7,
            $"product stock must be 7 after buying 3, got {productAfterCheckout.Stock}");

        var cancelled = await orderService.CancelAsync(order.Id, userId);
        CheckRunner.Ensure(cancelled.Success, cancelled.Error ?? "cancel failed");

        var productAfterCancel = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == product.Id);

        CheckRunner.Ensure(
            productAfterCancel.Stock == 10,
            $"product stock must be restored to 10, got {productAfterCancel.Stock}");
    }

    private static async Task CleanupAsync(
        AppDbContext dbContext,
        Guid userId,
        IReadOnlyCollection<int> productIds,
        long balanceAmount,
        int balanceVersion,
        DateTime startedAt)
    {
        // Удаляются только строки, созданные этой проверкой.
        dbContext.ChangeTracker.Clear();

        var createdOrders = await dbContext.Orders
            .Where(order => order.UserId == userId && order.CreatedAt >= startedAt)
            .Select(order => order.Id)
            .ToListAsync();

        if (createdOrders.Count > 0)
        {
            await dbContext.OrderItems
                .Where(item => createdOrders.Contains(item.OrderId))
                .ExecuteDeleteAsync();

            await dbContext.Orders
                .Where(order => createdOrders.Contains(order.Id))
                .ExecuteDeleteAsync();
        }

        if (productIds.Count > 0)
        {
            await dbContext.CartItems
                .Where(item => item.UserId == userId && productIds.Contains(item.ProductId))
                .ExecuteDeleteAsync();

            await dbContext.ProductVariants
                .Where(variant => productIds.Contains(variant.ProductId))
                .ExecuteDeleteAsync();

            await dbContext.Products
                .Where(product => productIds.Contains(product.Id))
                .ExecuteDeleteAsync();
        }

        await dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userId
                && transaction.CreatedAt >= startedAt
                && (transaction.RelatedEntityType == "order"
                    || transaction.RelatedEntityType == "achievement"))
            .ExecuteDeleteAsync();

        await dbContext.UserAchievements
            .Where(achievement => achievement.UserId == userId && achievement.EarnedAt >= startedAt)
            .ExecuteDeleteAsync();

        await dbContext.Balances
            .Where(item => item.UserId == userId && item.Type == BalanceType.Personal)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Amount, balanceAmount)
                .SetProperty(item => item.Version, balanceVersion));
    }
}
