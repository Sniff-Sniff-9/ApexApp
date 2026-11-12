using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class RewardAnalyticsService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IRewardAnalyticsService
{
    private static readonly TransactionType[] EarningTypes =
    [
        TransactionType.ActivityReward,
        TransactionType.Thanks,
        TransactionType.AchievementReward,
        TransactionType.ManagerBonus,
        TransactionType.DailyBonus
    ];

    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<RewardCatalogResult>> GetCatalogAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<RewardCatalogResult>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var result = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("rewards-catalog", range.CacheTag, AnalyticsCacheTags.Filter(null), AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.RewardTtl,
            token => BuildCatalogAsync(range, query.Top, token),
            cancellationToken);

        return ServiceResult<RewardCatalogResult>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<RewardVariantResult>> GetVariantsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<RewardVariantResult>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var result = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("rewards-variants", range.CacheTag, AnalyticsCacheTags.Filter(null), AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.RewardTtl,
            token => BuildVariantsAsync(range, query.Top, token),
            cancellationToken);

        return ServiceResult<RewardVariantResult>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyList<RewardCategoryRow>>> GetCategoriesAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<IReadOnlyList<RewardCategoryRow>>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var rows = await cache.GetOrCreateAsync<IReadOnlyList<RewardCategoryRow>>(
            CacheKeys.Analytics("rewards-categories", range.CacheTag, AnalyticsCacheTags.Filter(null), AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.RewardTtl,
            token => BuildCategoriesAsync(range, token),
            cancellationToken);

        return ServiceResult<IReadOnlyList<RewardCategoryRow>>.Ok(rows);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<RewardEconomy>> GetEconomyAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<RewardEconomy>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var economy = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("rewards-economy", range.CacheTag, AnalyticsCacheTags.Filter(null), AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.RewardTtl,
            token => BuildEconomyAsync(range, token),
            cancellationToken);

        return ServiceResult<RewardEconomy>.Ok(economy);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<OrderAnalytics>> GetOrdersAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<OrderAnalytics>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var orders = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("rewards-orders", range.CacheTag, AnalyticsCacheTags.Filter(null), AnalyticsCacheTags.InstantBucket(range.AsOf)),
            _options.RewardTtl,
            token => BuildOrdersAsync(range, token),
            cancellationToken);

        return ServiceResult<OrderAnalytics>.Ok(orders);
    }

    private async Task<RewardVariantResult> BuildVariantsAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken)
    {
        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Select(variant => new
            {
                variant.Id,
                variant.ProductId,
                ProductName = variant.Product.Name,
                ProductPrice = variant.Product.Price,
                variant.Name,
                variant.CategoryId,
                variant.Price,
                variant.Stock,
                variant.IsActive
            })
            .ToListAsync(cancellationToken);

        var categories = await dbContext.ProductVariantCategories
            .AsNoTracking()
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var demand = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.ProductVariantId != null
                && item.Order.Status != OrderStatus.Cancelled
                && item.Order.Status != OrderStatus.Refunded
                && item.Order.CreatedAt >= range.From
                && item.Order.CreatedAt < range.To)
            .GroupBy(item => item.ProductVariantId!.Value)
            .Select(group => new
            {
                VariantId = group.Key,
                Units = group.Sum(item => (long)item.Quantity),
                Lines = group.Count(),
                Points = group.Sum(item => (long)item.Quantity * item.PriceAtPurchase),
                Buyers = group.Select(item => item.Order.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var demandByVariant = demand.ToDictionary(row => row.VariantId);

        var cartUnits = await dbContext.CartItems
            .AsNoTracking()
            .Where(item => item.ProductVariantId != null)
            .GroupBy(item => item.ProductVariantId!.Value)
            .Select(group => new
            {
                VariantId = group.Key,
                Units = group.Sum(item => (long)item.Quantity)
            })
            .ToListAsync(cancellationToken);

        var cartByVariant = cartUnits.ToDictionary(row => row.VariantId, row => row.Units);

        var rows = new List<RewardVariantRow>(variants.Count);

        foreach (var variant in variants)
        {
            var demandRow = demandByVariant.GetValueOrDefault(variant.Id);
            var units = demandRow?.Units ?? 0;
            var stock = variant.Stock;
            var pool = units + stock;

            rows.Add(new RewardVariantRow(
                variant.Id,
                variant.ProductId,
                variant.ProductName,
                variant.Name,
                variant.CategoryId is int categoryId && categories.TryGetValue(categoryId, out var categoryName)
                    ? categoryName
                    : null,
                variant.Price ?? variant.ProductPrice,
                variant.IsActive,
                units,
                demandRow?.Lines ?? 0,
                demandRow?.Buyers ?? 0,
                demandRow?.Points ?? 0,
                stock,
                pool > 0 ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(units, pool), 3) : null,
                cartByVariant.GetValueOrDefault(variant.Id),
                stock == 0));
        }

        var ordered = rows
            .OrderByDescending(row => row.Units)
            .ThenByDescending(row => row.PointsValue)
            .ThenBy(row => row.ProductName, StringComparer.Ordinal)
            .ThenBy(row => row.VariantName, StringComparer.Ordinal)
            .ToList();

        var limited = top == int.MaxValue
            ? ordered
            : ordered.Take(Math.Clamp(top, 1, _options.MaxTop)).ToList();

        var caveats = new List<string>(range.Caveats)
        {
            "Вариант учитывается по order_items.product_variant_id: у заказов, оформленных до поддержки вариантов, это поле пустое.",
            "Цена варианта берётся из текущего справочника, а фактически оплаченная цена хранится в order_items.price_at_purchase.",
            "Для строк с вариантом уменьшается остаток варианта; остаток товара в этом случае не меняется."
        };

        return new RewardVariantResult(limited, ordered.Count, range, caveats);
    }

    private async Task<RewardCatalogResult> BuildCatalogAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.CategoryId,
                product.IsActive,
                product.IsDigital,
                product.Price,
                product.Stock
            })
            .ToListAsync(cancellationToken);

        var demand = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status != OrderStatus.Cancelled
                && item.Order.Status != OrderStatus.Refunded
                && item.Order.CreatedAt >= range.From
                && item.Order.CreatedAt < range.To)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Units = group.Sum(item => (long)item.Quantity),
                // Остаток товара расходуется только строками без варианта.
                PlainUnits = group.Sum(item => item.ProductVariantId == null ? (long)item.Quantity : 0),
                Lines = group.Count(),
                Points = group.Sum(item => (long)item.Quantity * item.PriceAtPurchase),
                Buyers = group.Select(item => item.Order.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var carts = await dbContext.CartItems
            .AsNoTracking()
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Units = group.Sum(item => (long)item.Quantity)
            })
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var demandByProduct = demand
            .Where(row => row.ProductId is not null)
            .ToDictionary(row => row.ProductId!.Value);

        var demandByNullProduct = demand.FirstOrDefault(row => row.ProductId is null);
        var cartByProduct = carts.ToDictionary(row => row.ProductId, row => row.Units);

        var rows = new List<RewardCatalogRow>(products.Count + 1);

        foreach (var product in products)
        {
            var demandRow = demandByProduct.GetValueOrDefault(product.Id);
            var units = demandRow?.Units ?? 0;
            var stock = product.Stock;
            double? sellThrough = null;

            if (stock is not null)
            {
                var pool = (demandRow?.PlainUnits ?? 0) + stock.Value;

                if (pool > 0)
                {
                    sellThrough = AnalyticsMetrics.Round(
                        AnalyticsMetrics.Ratio(demandRow?.PlainUnits ?? 0, pool),
                        3);
                }
            }

            rows.Add(new RewardCatalogRow(
                product.Id,
                product.Name,
                product.CategoryId,
                product.CategoryId is int categoryId && categories.TryGetValue(categoryId, out var categoryName)
                    ? categoryName
                    : null,
                product.IsActive,
                product.IsDigital,
                product.Price,
                units,
                demandRow?.Lines ?? 0,
                demandRow?.Buyers ?? 0,
                demandRow?.Points ?? 0,
                stock,
                sellThrough,
                cartByProduct.GetValueOrDefault(product.Id),
                stock == 0));
        }

        if (demandByNullProduct is not null && demandByNullProduct.Units > 0)
        {
            rows.Add(new RewardCatalogRow(
                null,
                "Товар удалён из каталога",
                null,
                null,
                false,
                false,
                0,
                demandByNullProduct.Units,
                demandByNullProduct.Lines,
                demandByNullProduct.Buyers,
                demandByNullProduct.Points,
                null,
                null,
                0,
                false));
        }

        var ordered = rows
            .OrderByDescending(row => row.Units)
            .ThenByDescending(row => row.PointsValue)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();

        var limited = top == int.MaxValue
            ? ordered
            : ordered.Take(Math.Clamp(top, 1, _options.MaxTop)).ToList();

        var caveats = new List<string>(range.Caveats)
        {
            "Для товаров с вариантами остаток ведётся по вариантам: доля продаж по товару считается только по строкам без варианта."
        };

        return new RewardCatalogResult(
            limited,
            ordered.Count,
            range,
            caveats);
    }

    private async Task<IReadOnlyList<RewardCategoryRow>> BuildCategoriesAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var catalog = await BuildCatalogAsync(range, int.MaxValue, cancellationToken);
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(cancellationToken);

        var knownCategories = categories.ToDictionary(category => category.Id);

        var productsByCategory = await dbContext.Products
            .AsNoTracking()
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var productCounts = productsByCategory
            .Where(row => row.CategoryId is not null)
            .ToDictionary(row => row.CategoryId!.Value, row => (long)row.Count);

        var rows = new List<RewardCategoryRow>();

        foreach (var group in catalog.Items.GroupBy(row => row.CategoryId))
        {
            var categoryId = group.Key;
            var name = categoryId is int id
                ? knownCategories.TryGetValue(id, out var category) ? category.Name : $"Категория {id}"
                : "Без категории";

            var units = group.Sum(item => item.Units);
            var buyers = group.Sum(item => item.UniqueBuyers);

            rows.Add(new RewardCategoryRow(
                categoryId,
                name,
                categoryId is int knownId
                    ? productCounts.GetValueOrDefault(knownId)
                    : group.Count(item => item.ProductId is null),
                units,
                group.Sum(item => item.PointsValue),
                buyers,
                buyers >= _options.MinCohort));
        }

        return rows
            .OrderByDescending(row => row.Units)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<RewardEconomy> BuildEconomyAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var byType = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                EarningTypes.Contains(transaction.Type)
                && transaction.CreatedAt >= range.From
                && transaction.CreatedAt < range.To)
            .GroupBy(transaction => transaction.Type)
            .Select(group => new
            {
                Type = group.Key,
                Points = group.Sum(transaction => (long?)transaction.Amount) ?? 0,
                Users = group.Select(transaction => transaction.UserId).Distinct().Count(),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var bySource = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.ActivityReward
                && transaction.RelatedEntityType != null
                && transaction.CreatedAt >= range.From
                && transaction.CreatedAt < range.To)
            .GroupBy(transaction => transaction.RelatedEntityType!)
            .Select(group => new
            {
                Source = group.Key,
                Points = group.Sum(transaction => (long?)transaction.Amount) ?? 0,
                Users = group.Select(transaction => transaction.UserId).Distinct().Count(),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var balances = await dbContext.Balances
            .AsNoTracking()
            .GroupBy(balance => balance.Type)
            .Select(group => new
            {
                Type = group.Key,
                Amount = group.Sum(balance => (long?)balance.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        var ordersPoints = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status != OrderStatus.Cancelled
                && order.Status != OrderStatus.Refunded
                && order.CreatedAt >= range.From
                && order.CreatedAt < range.To)
            .SumAsync(order => (long?)order.TotalAmount, cancellationToken) ?? 0;

        var allOrdersPoints = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= range.From && order.CreatedAt < range.To)
            .SumAsync(order => (long?)order.TotalAmount, cancellationToken) ?? 0;

        var ledgerSpent = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.Purchase
                && transaction.CreatedAt >= range.From
                && transaction.CreatedAt < range.To)
            .SumAsync(transaction => (long?)-transaction.Amount, cancellationToken) ?? 0;

        var earnedTotal = byType.Sum(row => row.Points);
        var personal = balances.FirstOrDefault(row => row.Type == BalanceType.Personal)?.Amount ?? 0;
        var reward = balances.FirstOrDefault(row => row.Type == BalanceType.Reward)?.Amount ?? 0;

        var typeRows = byType
            .Select(row => new PointsFlowRow(
                row.Type.ToString(),
                TransactionTypeTitle(row.Type),
                row.Points,
                row.Users,
                row.Count))
            .OrderByDescending(row => row.Points)
            .ToList();

        var sourceRows = bySource
            .Select(row => new PointsFlowRow(
                row.Source,
                SourceTitle(row.Source),
                row.Points,
                row.Users,
                row.Count))
            .OrderByDescending(row => row.Points)
            .ToList();

        var tolerance = Math.Max(1, (long)Math.Round(ledgerSpent * 0.01d, MidpointRounding.AwayFromZero));

        var caveats = new List<string>(range.Caveats)
        {
            "Все суммы выражены во внутренних баллах, а не в деньгах.",
            "Начислением считаются activity_reward, thanks, achievement_reward, manager_bonus и daily_bonus; " +
            "ручные корректировки администратора не входят в начисления за активность."
        };

        return new RewardEconomy(
            typeRows,
            sourceRows,
            earnedTotal,
            ordersPoints,
            personal,
            reward,
            AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(ordersPoints, earnedTotal), 3),
            ordersPoints,
            ledgerSpent,
            Math.Abs(allOrdersPoints - ledgerSpent) <= tolerance,
            range,
            caveats);
    }

    private async Task<OrderAnalytics> BuildOrdersAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var statusRows = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= range.From && order.CreatedAt < range.To)
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Orders = group.Count(),
                Points = group.Sum(order => (long?)order.TotalAmount) ?? 0
            })
            .ToListAsync(cancellationToken);

        var effective = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status != OrderStatus.Cancelled
                && order.Status != OrderStatus.Refunded
                && order.CreatedAt >= range.From
                && order.CreatedAt < range.To);

        var orders = await effective.LongCountAsync(cancellationToken);
        var buyers = await effective.Select(order => order.UserId).Distinct().CountAsync(cancellationToken);

        var units = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status != OrderStatus.Cancelled
                && item.Order.Status != OrderStatus.Refunded
                && item.Order.CreatedAt >= range.From
                && item.Order.CreatedAt < range.To)
            .SumAsync(item => (long?)item.Quantity, cancellationToken) ?? 0;

        var points = await effective.SumAsync(order => (long?)order.TotalAmount, cancellationToken) ?? 0;

        var processingRows = await dbContext.Database.SqlQuery<ScalarDoubleRow>($"""
            SELECT AVG(EXTRACT(EPOCH FROM (o.updated_at - o.created_at)) / 3600.0) AS "Value"
            FROM public.orders o
            WHERE o.status = 'delivered'::order_status
              AND o.created_at >= {range.From}
              AND o.created_at < {range.To}
            """).ToListAsync(cancellationToken);

        var byStatus = statusRows
            .Select(row => new OrderStatusRow(
                row.Status.ToString(),
                OrderStatusTitle(row.Status),
                row.Orders,
                row.Points))
            .OrderByDescending(row => row.Orders)
            .ToList();

        var caveats = new List<string>(range.Caveats)
        {
            "Стоимость заказа выражена во внутренних баллах.",
            "Время обработки измеряется от создания до последнего изменения статуса: " +
            "история статусов не хранится, поэтому значение приблизительное."
        };

        return new OrderAnalytics(
            orders,
            units,
            buyers,
            points,
            orders > 0 ? AnalyticsMetrics.Round((double)points / orders, 1) : null,
            processingRows.Count == 0 ? null : AnalyticsMetrics.Round(processingRows[0].Value, 1),
            statusRows.FirstOrDefault(row => row.Status == OrderStatus.Cancelled)?.Orders ?? 0,
            statusRows.FirstOrDefault(row => row.Status == OrderStatus.Delivered)?.Orders ?? 0,
            byStatus,
            range,
            caveats);
    }

    private static string TransactionTypeTitle(TransactionType type) => type switch
    {
        TransactionType.ActivityReward => "Награда за активность",
        TransactionType.Thanks => "Благодарность",
        TransactionType.AchievementReward => "Награда за достижение",
        TransactionType.ManagerBonus => "Бонус руководителя",
        TransactionType.DailyBonus => "Ежедневный бонус",
        TransactionType.Purchase => "Покупка",
        TransactionType.Refund => "Возврат",
        TransactionType.AdminAdjustment => "Корректировка администратора",
        _ => "Прочее"
    };

    private static string SourceTitle(string source) => source.ToLowerInvariant() switch
    {
        "quest" => "Задания",
        "event" => "Мероприятия",
        "survey" => "Опросы",
        _ => source
    };

    private static string OrderStatusTitle(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Ожидает оплаты",
        OrderStatus.Paid => "Оплачен",
        OrderStatus.Processing => "В обработке",
        OrderStatus.Ready => "Готов к выдаче",
        OrderStatus.Delivered => "Выдан",
        OrderStatus.Cancelled => "Отменён",
        _ => "Возвращён"
    };
}
