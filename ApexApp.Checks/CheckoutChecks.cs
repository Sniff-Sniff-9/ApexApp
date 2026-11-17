using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexApp.Checks;

/// <summary>
/// End-to-end checks of the checkout flow against the real database: a purchase with
/// several variants of one product and a plain purchase without a variant.
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
        await runner.CheckAsync("checkout.multi-variant-price-stock-and-history", async () =>
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
        // за первый заказ во время проверки. Корзина должна быть пустой, иначе
        // checkout захватит чужие строки, а баланса должно хватить на три строки.
        // 135 = 3*25 + 1*40 + 2*10 — сумма, которую сценарий ниже оплатит.
        var userId = await dbContext.Balances
            .Where(balance =>
                balance.Type == BalanceType.Personal
                && balance.Amount >= 135
                && dbContext.Orders.Any(order =>
                    order.UserId == balance.UserId
                    && order.Status == OrderStatus.Paid)
                && !dbContext.CartItems.Any(item => item.UserId == balance.UserId))
            .OrderByDescending(balance => balance.Amount)
            .Select(balance => balance.UserId)
            .FirstOrDefaultAsync();

        CheckRunner.Ensure(
            userId != Guid.Empty,
            "no user with a paid order, an empty cart, and at least 135 points is available for the checkout check");

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

        // Повторное добавление того же варианта должно увеличить существующую строку.
        var added = await cartService.AddAsync(userId, product.Id, variant.Id, 2);
        CheckRunner.Ensure(added.Success, added.Error ?? "add to cart failed");

        var repeated = await cartService.AddAsync(userId, product.Id, variant.Id, 1);
        CheckRunner.Ensure(repeated.Success, repeated.Error ?? "repeat add to cart failed");

        // Другой вариант должен создать отдельную строку.
        var secondVariant = await cartService.AddAsync(userId, product.Id, otherVariant.Id, 1);
        CheckRunner.Ensure(secondVariant.Success, secondVariant.Error ?? "second variant add failed");

        // NULL-вариант — самостоятельный выбор, который тоже схлопывается при повторе.
        var plain = await cartService.AddAsync(userId, product.Id, null, 1);
        CheckRunner.Ensure(plain.Success, plain.Error ?? "plain add failed");

        var plainRepeated = await cartService.AddAsync(userId, product.Id, null, 1);
        CheckRunner.Ensure(plainRepeated.Success, plainRepeated.Error ?? "plain repeat add failed");

        var unknownVariant = await cartService.AddAsync(userId, product.Id, 999_999, 1);
        CheckRunner.Ensure(!unknownVariant.Success, "an unknown variant must be rejected");

        var cart = await cartService.GetAsync(userId);
        var productRows = cart.Where(item => item.ProductId == product.Id).ToList();

        CheckRunner.Ensure(
            productRows.Count == 3,
            $"three cart rows expected for two variants plus the plain item, got {productRows.Count}");

        var variantRow = productRows.Single(item => item.ProductVariantId == variant.Id);
        var otherVariantRow = productRows.Single(item => item.ProductVariantId == otherVariant.Id);
        var plainRow = productRows.Single(item => item.ProductVariantId is null);

        CheckRunner.Ensure(
            variantRow.Quantity == 3,
            $"the repeated variant quantity must be 3, got {variantRow.Quantity}");
        CheckRunner.Ensure(
            otherVariantRow.Quantity == 1,
            $"the other variant quantity must be 1, got {otherVariantRow.Quantity}");
        CheckRunner.Ensure(
            plainRow.Quantity == 2,
            $"the repeated plain quantity must be 2, got {plainRow.Quantity}");

        // Обновление количества одной строки не должно менять остальные варианты.
        var updated = await cartService.UpdateAsync(userId, otherVariantRow.Id, null, 2);
        CheckRunner.Ensure(updated.Success, updated.Error ?? "quantity update failed");

        var afterUpdate = await cartService.GetAsync(userId);

        CheckRunner.Ensure(
            afterUpdate.Single(item => item.Id == variantRow.Id).Quantity == 3,
            "updating another variant must not change the first variant row");
        CheckRunner.Ensure(
            afterUpdate.Single(item => item.Id == otherVariantRow.Id).Quantity == 2,
            "the updated variant row must have quantity 2");
        CheckRunner.Ensure(
            afterUpdate.Single(item => item.Id == plainRow.Id).Quantity == 2,
            "updating a variant must not change the plain row");

        // Удаление одной строки не должно затрагивать остальные варианты.
        var removed = await cartService.RemoveAsync(userId, otherVariantRow.Id);
        CheckRunner.Ensure(removed.Success, removed.Error ?? "remove failed");

        var afterRemove = await cartService.GetAsync(userId);
        var remainingRows = afterRemove.Where(item => item.ProductId == product.Id).ToList();

        CheckRunner.Ensure(
            remainingRows.Count == 2,
            $"two cart rows must remain after removing one variant, got {remainingRows.Count}");
        CheckRunner.Ensure(
            remainingRows.All(item => item.Id != otherVariantRow.Id),
            "the removed variant row must be gone");
        CheckRunner.Ensure(
            remainingRows.Any(item => item.Id == variantRow.Id)
            && remainingRows.Any(item => item.Id == plainRow.Id),
            "removing one variant must keep the other variant and the plain row");

        // Возвращаем удалённый вариант, чтобы checkout обработал все три строки.
        var reAdded = await cartService.AddAsync(userId, product.Id, otherVariant.Id, 1);
        CheckRunner.Ensure(reAdded.Success, reAdded.Error ?? "re-adding the variant failed");

        var checkout = await orderService.CheckoutAsync(userId);
        CheckRunner.Ensure(checkout.Success, checkout.Error ?? "checkout failed");

        var order = checkout.Data!;

        CheckRunner.Ensure(
            order.OrderItems.Count == 3,
            $"three order items expected, got {order.OrderItems.Count}");

        var variantItem = order.OrderItems.Single(item => item.ProductVariantId == variant.Id);
        var otherVariantItem = order.OrderItems.Single(item => item.ProductVariantId == otherVariant.Id);
        var plainItem = order.OrderItems.Single(item => item.ProductVariantId is null);

        CheckRunner.Ensure(
            variantItem.Quantity == 3 && variantItem.PriceAtPurchase == 25,
            "the first variant must keep quantity 3 and its own price");
        CheckRunner.Ensure(
            otherVariantItem.Quantity == 1 && otherVariantItem.PriceAtPurchase == 40,
            "the second variant must keep quantity 1 and its own price");
        CheckRunner.Ensure(
            plainItem.Quantity == 2 && plainItem.PriceAtPurchase == 10,
            "the plain row must keep quantity 2 and the product price");
        CheckRunner.Ensure(
            order.TotalAmount == 135,
            $"order total must be 3*25 + 1*40 + 2*10 = 135 points, got {order.TotalAmount}");

        var history = await orderService.GetHistoryAsync(userId);
        var historyOrder = history.Single(entry => entry.Id == order.Id);
        var historyVariantItem = historyOrder.OrderItems
            .Single(item => item.ProductVariantId == variant.Id);

        CheckRunner.Ensure(
            historyVariantItem.ProductVariant?.Name == "Вариант A",
            "the order history must expose the variant");

        var variantAfterCheckout = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == variant.Id);
        var otherVariantAfterCheckout = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == otherVariant.Id);
        var productAfterCheckout = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == product.Id);

        CheckRunner.Ensure(
            variantAfterCheckout.Stock == 2,
            $"the first variant stock must be 2 after buying 3, got {variantAfterCheckout.Stock}");
        CheckRunner.Ensure(
            otherVariantAfterCheckout.Stock == 4,
            $"the second variant stock must be 4 after buying 1, got {otherVariantAfterCheckout.Stock}");
        CheckRunner.Ensure(
            productAfterCheckout.Stock == 48,
            $"product stock must be 48 after buying 2 plain units, got {productAfterCheckout.Stock}");

        var cancelled = await orderService.CancelAsync(order.Id, userId);
        CheckRunner.Ensure(cancelled.Success, cancelled.Error ?? "cancel failed");

        var variantAfterCancel = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == variant.Id);
        var otherVariantAfterCancel = await dbContext.ProductVariants
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == otherVariant.Id);
        var productAfterCancel = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == product.Id);

        CheckRunner.Ensure(
            variantAfterCancel.Stock == 5,
            $"the first variant stock must be restored to 5, got {variantAfterCancel.Stock}");
        CheckRunner.Ensure(
            otherVariantAfterCancel.Stock == 5,
            $"the second variant stock must be restored to 5, got {otherVariantAfterCancel.Stock}");
        CheckRunner.Ensure(
            productAfterCancel.Stock == 50,
            $"product stock must be restored to 50, got {productAfterCancel.Stock}");
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

        var startedAtOffset = new DateTimeOffset(startedAt, TimeSpan.Zero);

        await dbContext.Notifications
            .Where(notification =>
                notification.UserId == userId &&
                notification.CreatedAt >= startedAtOffset)
            .ExecuteDeleteAsync();

        await dbContext.Balances
            .Where(item => item.UserId == userId && item.Type == BalanceType.Personal)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Amount, balanceAmount)
                .SetProperty(item => item.Version, balanceVersion));
    }
}
