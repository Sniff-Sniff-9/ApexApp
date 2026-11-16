using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Recalculates automatic badge assignments from current catalog and sales data.
/// The operation is idempotent: qualifying rows are inserted or extended, while
/// rows that no longer satisfy the rule are expired or removed.
/// </summary>
public sealed class BadgeMaintenanceService(
    AppDbContext dbContext,
    ICacheService cache,
    ILogger<BadgeMaintenanceService> logger) : IBadgeMaintenanceService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task RecalculateAllAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            var badges = await LoadAutomaticBadgesAsync(cancellationToken);

            if (badges.Count == 0)
            {
                return;
            }

            foreach (var badge in badges)
            {
                await RecalculateBadgeCoreAsync(badge, productIds: null, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task RecalculateForProductAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken);

        try
        {
            var badges = await LoadAutomaticBadgesAsync(cancellationToken);

            if (badges.Count == 0)
            {
                return;
            }

            foreach (var badge in badges)
            {
                await RecalculateBadgeCoreAsync(
                    badge,
                    new[] { productId },
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task RecalculateBadgeAsync(
        string badgeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badgeCode))
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken);

        try
        {
            var code = badgeCode.Trim().ToUpperInvariant();

            var badge = await dbContext.ProductBadges
                .AsNoTracking()
                .Include(item => item.Settings)
                .FirstOrDefaultAsync(
                    item => item.Code == code,
                    cancellationToken);

            if (badge is null)
            {
                return;
            }

            await RecalculateBadgeCoreAsync(badge, productIds: null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<List<ProductBadge>> LoadAutomaticBadgesAsync(
        CancellationToken cancellationToken)
    {
        var badges = await dbContext.ProductBadges
            .AsNoTracking()
            .Include(badge => badge.Settings)
            .Where(badge => badge.IsActive)
            .ToListAsync(cancellationToken);

        return badges
            .Where(badge => string.Equals(
                badge.AssignmentType,
                ProductBadgeAssignmentTypes.Automatic,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task RecalculateBadgeCoreAsync(
        ProductBadge badge,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        if (!badge.IsActive ||
            badge.Settings is null ||
            !badge.Settings.IsEnabled)
        {
            return;
        }

        var code = badge.Code.Trim().ToUpperInvariant();

        try
        {
            switch (code)
            {
                case ProductBadgeCodes.Hit:
                    await RecalculateHitAsync(badge, productIds, cancellationToken);
                    break;

                case ProductBadgeCodes.New:
                    await RecalculateNewAsync(badge, productIds, cancellationToken);
                    break;

                case ProductBadgeCodes.LowStock:
                    await RecalculateLowStockAsync(badge, productIds, cancellationToken);
                    break;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to recalculate automatic badge {BadgeCode}",
                badge.Code);
        }
    }

    private async Task RecalculateHitAsync(
        ProductBadge badge,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        var settings = badge.Settings!;
        var periodDays = Math.Max(settings.PeriodDays ?? 30, 1);
        var threshold = Math.Max(settings.Threshold ?? 1, 1);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-periodDays);

        var query = dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.ProductId != null &&
                item.Order.Status != OrderStatus.Cancelled &&
                item.Order.Status != OrderStatus.Pending &&
                item.Order.Status != OrderStatus.Refunded &&
                item.Order.CreatedAt >= cutoff);

        if (productIds is { Count: > 0 })
        {
            query = query.Where(item => productIds.Contains(item.ProductId!.Value));
        }

        var rows = await query
            .GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductVariantId,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToListAsync(cancellationToken);

        var qualifying = rows
            .Where(row => row.ProductId is not null && row.Quantity >= threshold)
            .Select(row => new BadgeSkuKey(row.ProductId!.Value, row.ProductVariantId))
            .ToHashSet();

        await ApplyQualificationAsync(
            badge,
            qualifying,
            startsAt: now,
            endsAt: settings.DurationDays is int durationDays
                ? now.AddDays(Math.Max(durationDays, 1))
                : null,
            removeNonQualifying: settings.DurationDays is null,
            productIds,
            cancellationToken);
    }

    private async Task RecalculateNewAsync(
        ProductBadge badge,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        var settings = badge.Settings!;
        var periodDays = Math.Max(settings.PeriodDays ?? 14, 1);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-periodDays);

        var query = dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsActive &&
                !product.IsDigital &&
                product.CreatedAt >= cutoff);

        if (productIds is { Count: > 0 })
        {
            query = query.Where(product => productIds.Contains(product.Id));
        }

        var products = await query
            .Select(product => new { product.Id, product.CreatedAt })
            .ToListAsync(cancellationToken);

        var qualifying = products
            .Select(product => new BadgeSkuKey(product.Id, null))
            .ToHashSet();

        // NEW is a product-level badge whose end is fixed by the product creation date.
        foreach (var product in products)
        {
            var existing = await dbContext.ProductBadgeAssignments
                .FirstOrDefaultAsync(
                    assignment =>
                        assignment.BadgeId == badge.Id &&
                        assignment.ProductId == product.Id &&
                        assignment.ProductVariantId == null,
                    cancellationToken);

            var startsAt = UtcDateTime.Normalize(product.CreatedAt);
            var endsAt = startsAt.AddDays(periodDays);

            if (existing is null)
            {
                dbContext.ProductBadgeAssignments.Add(new ProductBadgeAssignment
                {
                    ProductId = product.Id,
                    ProductVariantId = null,
                    BadgeId = badge.Id,
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    CreatedAt = now
                });
            }
            else
            {
                existing.StartsAt = startsAt;
                existing.EndsAt = endsAt;
            }
        }

        await RemoveNonQualifyingAsync(
            badge,
            qualifying,
            removeAll: false,
            productIds,
            cancellationToken);
    }

    private async Task RecalculateLowStockAsync(
        ProductBadge badge,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        var settings = badge.Settings!;
        var threshold = Math.Max(settings.Threshold ?? 0, 0);
        var now = DateTime.UtcNow;

        var productQuery = dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && !product.IsDigital);

        if (productIds is { Count: > 0 })
        {
            productQuery = productQuery.Where(product => productIds.Contains(product.Id));
        }

        var products = await productQuery
            .Select(product => new { product.Id, product.Stock })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return;
        }

        var ids = products.Select(product => product.Id).ToList();

        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.IsActive && ids.Contains(variant.ProductId))
            .Select(variant => new
            {
                variant.Id,
                variant.ProductId,
                variant.Stock
            })
            .ToListAsync(cancellationToken);

        var variantsByProduct = variants
            .GroupBy(variant => variant.ProductId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var qualifying = new HashSet<BadgeSkuKey>();

        foreach (var product in products)
        {
            if (variantsByProduct.TryGetValue(product.Id, out var productVariants) &&
                productVariants.Count > 0)
            {
                foreach (var variant in productVariants)
                {
                    if (variant.Stock <= threshold)
                    {
                        qualifying.Add(new BadgeSkuKey(product.Id, variant.Id));
                    }
                }

                continue;
            }

            if (product.Stock is int stock && stock <= threshold)
            {
                qualifying.Add(new BadgeSkuKey(product.Id, null));
            }
        }

        await ApplyQualificationAsync(
            badge,
            qualifying,
            startsAt: now,
            endsAt: null,
            removeNonQualifying: true,
            productIds,
            cancellationToken);
    }

    private async Task ApplyQualificationAsync(
        ProductBadge badge,
        IReadOnlySet<BadgeSkuKey> qualifying,
        DateTime startsAt,
        DateTime? endsAt,
        bool removeNonQualifying,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProductBadgeAssignments
            .Where(assignment => assignment.BadgeId == badge.Id);

        if (productIds is { Count: > 0 })
        {
            query = query.Where(assignment => productIds.Contains(assignment.ProductId));
        }

        var existing = await query.ToListAsync(cancellationToken);

        var byKey = new Dictionary<BadgeSkuKey, ProductBadgeAssignment>();

        foreach (var assignment in existing)
        {
            var key = new BadgeSkuKey(
                assignment.ProductId,
                assignment.ProductVariantId);

            if (byKey.TryAdd(key, assignment))
            {
                continue;
            }

            // Older data may contain duplicate assignments. Keep the first one and
            // remove the rest so the recalculation remains deterministic.
            dbContext.ProductBadgeAssignments.Remove(assignment);
        }

        foreach (var key in qualifying)
        {
            if (!byKey.TryGetValue(key, out var assignment))
            {
                dbContext.ProductBadgeAssignments.Add(new ProductBadgeAssignment
                {
                    ProductId = key.ProductId,
                    ProductVariantId = key.ProductVariantId,
                    BadgeId = badge.Id,
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    CreatedAt = DateTime.UtcNow
                });

                continue;
            }

            if (assignment.StartsAt is null || assignment.StartsAt > startsAt)
            {
                assignment.StartsAt = startsAt;
            }

            if (endsAt is null)
            {
                assignment.EndsAt = null;
            }
            else if (assignment.EndsAt is null || assignment.EndsAt < endsAt)
            {
                assignment.EndsAt = endsAt;
            }
        }

        if (!removeNonQualifying)
        {
            return;
        }

        foreach (var pair in byKey)
        {
            if (qualifying.Contains(pair.Key))
            {
                continue;
            }

            if (removeNonQualifying)
            {
                dbContext.ProductBadgeAssignments.Remove(pair.Value);
            }
            else if (pair.Value.EndsAt is null)
            {
                // A HIT badge with a configured duration must not stay forever
                // after its sales window has dropped below the threshold.
                pair.Value.EndsAt = DateTime.UtcNow;
            }
        }
    }

    private async Task RemoveNonQualifyingAsync(
        ProductBadge badge,
        IReadOnlySet<BadgeSkuKey> qualifying,
        bool removeAll,
        IReadOnlyCollection<int>? productIds,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProductBadgeAssignments
            .Where(assignment => assignment.BadgeId == badge.Id);

        if (productIds is { Count: > 0 })
        {
            query = query.Where(assignment => productIds.Contains(assignment.ProductId));
        }

        var assignments = await query.ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            var key = new BadgeSkuKey(
                assignment.ProductId,
                assignment.ProductVariantId);

            if (removeAll || !qualifying.Contains(key))
            {
                dbContext.ProductBadgeAssignments.Remove(assignment);
            }
        }
    }

    private void InvalidateCatalog()
    {
        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);
        cache.RemoveByPrefix(CacheKeys.CatalogPrefix);
    }

    private readonly record struct BadgeSkuKey(int ProductId, int? ProductVariantId);
}
