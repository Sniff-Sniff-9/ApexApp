using System.Text.RegularExpressions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Badge definitions, settings and manual assignments. Automatic assignments are
/// read-only here and are produced by <see cref="IBadgeMaintenanceService"/>.
/// </summary>
public sealed class ProductBadgeService(
    AppDbContext dbContext,
    ICacheService cache,
    IBadgeMaintenanceService badgeMaintenance) : IProductBadgeService
{
    private static readonly Regex ColorPattern = new(
        "^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<List<ProductBadge>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.ProductBadges
            .AsNoTracking()
            .Include(badge => badge.Settings)
            .Where(badge => badge.IsActive)
            .OrderBy(badge => badge.SortOrder)
            .ThenBy(badge => badge.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ProductBadge>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.ProductBadges
            .AsNoTracking()
            .Include(badge => badge.Settings)
            .OrderBy(badge => badge.SortOrder)
            .ThenBy(badge => badge.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<ProductBadge>> CreateAsync(
        CreateProductBadgeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<ProductBadge>.Fail(400, "A badge command is required.");
        }

        var code = command.Code?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge code is required.");
        }

        if (code.Length > 50)
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge code must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge name is required.");
        }

        if (!ColorPattern.IsMatch(command.Color ?? string.Empty))
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge color must be a #RRGGBB value.");
        }

        if (!ProductBadgeAssignmentTypes.IsValid(command.AssignmentType))
        {
            return ServiceResult<ProductBadge>.Fail(
                400,
                "Assignment type must be either \"manual\" or \"automatic\".");
        }

        var assignmentType = ProductBadgeAssignmentTypes.Normalize(command.AssignmentType);

        if (string.Equals(
                assignmentType,
                ProductBadgeAssignmentTypes.Automatic,
                StringComparison.Ordinal) &&
            code is not (ProductBadgeCodes.Hit or ProductBadgeCodes.New or ProductBadgeCodes.LowStock))
        {
            return ServiceResult<ProductBadge>.Fail(
                400,
                "Automatic badges are limited to HIT, NEW and LOW_STOCK.");
        }

        if (ProductBadgeCodes.SystemCodes.Contains(code))
        {
            return ServiceResult<ProductBadge>.Fail(
                409,
                "A system badge with this code already exists.");
        }

        var existingCodes = await dbContext.ProductBadges
            .AsNoTracking()
            .Select(badge => badge.Code)
            .ToListAsync(cancellationToken);

        var codeExists = existingCodes.Any(
            existingCode => string.Equals(
                existingCode,
                code,
                StringComparison.OrdinalIgnoreCase));

        if (codeExists)
        {
            return ServiceResult<ProductBadge>.Fail(
                409,
                "A badge with this code already exists.");
        }

        var settingsError = ValidateSettings(assignmentType, command.Settings);

        if (settingsError is not null)
        {
            return ServiceResult<ProductBadge>.Fail(400, settingsError);
        }

        var now = DateTime.UtcNow;

        var badge = new ProductBadge
        {
            Code = code,
            Name = command.Name.Trim(),
            Color = command.Color!.Trim(),
            AssignmentType = assignmentType,
            IsActive = command.IsActive,
            SortOrder = command.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProductBadges.Add(badge);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (command.Settings is not null)
        {
            var settings = new ProductBadgeSetting
            {
                BadgeId = badge.Id,
                PeriodDays = command.Settings.PeriodDays,
                Threshold = command.Settings.Threshold,
                DurationDays = command.Settings.DurationDays,
                IsEnabled = command.Settings.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.ProductBadgeSettings.Add(settings);
            badge.Settings = settings;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        InvalidateCatalog();

        return ServiceResult<ProductBadge>.Ok(badge);
    }

    public async Task<ServiceResult<ProductBadge>> UpdateAsync(
        int id,
        UpdateProductBadgeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<ProductBadge>.Fail(400, "A badge command is required.");
        }

        var badge = await dbContext.ProductBadges
            .Include(item => item.Settings)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (badge is null)
        {
            return ServiceResult<ProductBadge>.Fail(404, "Badge was not found.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge name is required.");
        }

        if (!ColorPattern.IsMatch(command.Color ?? string.Empty))
        {
            return ServiceResult<ProductBadge>.Fail(400, "Badge color must be a #RRGGBB value.");
        }

        if (!ProductBadgeAssignmentTypes.IsValid(command.AssignmentType))
        {
            return ServiceResult<ProductBadge>.Fail(
                400,
                "Assignment type must be either \"manual\" or \"automatic\".");
        }

        var assignmentType = ProductBadgeAssignmentTypes.Normalize(command.AssignmentType);
        var isSystem = ProductBadgeCodes.SystemCodes.Contains(badge.Code);
        var normalizedCode = badge.Code.Trim().ToUpperInvariant();

        if (string.Equals(
                assignmentType,
                ProductBadgeAssignmentTypes.Automatic,
                StringComparison.Ordinal) &&
            normalizedCode is not (ProductBadgeCodes.Hit or ProductBadgeCodes.New or ProductBadgeCodes.LowStock))
        {
            return ServiceResult<ProductBadge>.Fail(
                400,
                "Automatic badges are limited to HIT, NEW and LOW_STOCK.");
        }

        if (isSystem && !string.Equals(
                badge.AssignmentType,
                assignmentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ProductBadge>.Fail(
                409,
                "The assignment type of a system badge cannot be changed.");
        }

        if (!string.Equals(
                badge.AssignmentType,
                assignmentType,
                StringComparison.OrdinalIgnoreCase))
        {
            var hasAssignments = await dbContext.ProductBadgeAssignments
                .AnyAsync(assignment => assignment.BadgeId == id, cancellationToken);

            if (hasAssignments)
            {
                return ServiceResult<ProductBadge>.Fail(
                    409,
                    "The assignment type cannot be changed while assignments exist.");
            }
        }

        badge.Code = normalizedCode;
        badge.Name = command.Name.Trim();
        badge.Color = command.Color!.Trim();
        badge.AssignmentType = assignmentType;
        badge.IsActive = command.IsActive;
        badge.SortOrder = command.SortOrder;
        badge.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        return ServiceResult<ProductBadge>.Ok(badge);
    }

    public async Task<ServiceResult> DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var badge = await dbContext.ProductBadges
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (badge is null)
        {
            return ServiceResult.Fail(404, "Badge was not found.");
        }

        if (badge.IsActive)
        {
            badge.IsActive = false;
            badge.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<ProductBadgeSetting>> UpsertSettingsAsync(
        int badgeId,
        BadgeSettingsInput input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return ServiceResult<ProductBadgeSetting>.Fail(400, "Badge settings are required.");
        }

        var badge = await dbContext.ProductBadges
            .Include(item => item.Settings)
            .FirstOrDefaultAsync(item => item.Id == badgeId, cancellationToken);

        if (badge is null)
        {
            return ServiceResult<ProductBadgeSetting>.Fail(404, "Badge was not found.");
        }

        if (string.Equals(
                badge.AssignmentType,
                ProductBadgeAssignmentTypes.Manual,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ProductBadgeSetting>.Fail(
                400,
                "Manual badges do not use automatic settings.");
        }

        var settingsError = ValidateAutomaticSettings(badge.Code, input);

        if (settingsError is not null)
        {
            return ServiceResult<ProductBadgeSetting>.Fail(400, settingsError);
        }

        var now = DateTime.UtcNow;
        var settings = badge.Settings;

        if (settings is null)
        {
            settings = new ProductBadgeSetting
            {
                BadgeId = badge.Id,
                CreatedAt = now
            };

            dbContext.ProductBadgeSettings.Add(settings);
        }

        settings.PeriodDays = input.PeriodDays;
        settings.Threshold = input.Threshold;
        settings.DurationDays = input.DurationDays;
        settings.IsEnabled = input.IsEnabled;
        settings.UpdatedAt = now;

        badge.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        await badgeMaintenance.RecalculateBadgeAsync(badge.Code, cancellationToken);

        return ServiceResult<ProductBadgeSetting>.Ok(settings);
    }

    public async Task<ServiceResult<ProductBadgeAssignment>> AssignManualAsync(
        int productId,
        int badgeId,
        AssignProductBadgeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || badgeId <= 0)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(
                400,
                "Valid product and badge IDs are required.");
        }

        if (command is null)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(
                400,
                "A badge assignment command is required.");
        }

        var badge = await dbContext.ProductBadges
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == badgeId, cancellationToken);

        if (badge is null)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(404, "Badge was not found.");
        }

        if (!badge.IsActive)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(409, "Badge is inactive.");
        }

        if (!string.Equals(
                badge.AssignmentType,
                ProductBadgeAssignmentTypes.Manual,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(
                409,
                "Automatic badges cannot be assigned manually.");
        }

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken);

        if (!productExists)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(404, "Product was not found.");
        }

        if (command.ProductVariantId is int variantId)
        {
            var variantExists = await dbContext.ProductVariants
                .AsNoTracking()
                .AnyAsync(
                    variant => variant.Id == variantId && variant.ProductId == productId,
                    cancellationToken);

            if (!variantExists)
            {
                return ServiceResult<ProductBadgeAssignment>.Fail(
                    400,
                    "Product variant was not found for this product.");
            }
        }

        var startsAt = UtcDateTime.Normalize(command.StartsAt) ?? DateTime.UtcNow;
        var endsAt = UtcDateTime.Normalize(command.EndsAt);

        if (endsAt is not null && endsAt <= startsAt)
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(
                400,
                "EndsAt must be later than StartsAt.");
        }

        var existing = await dbContext.ProductBadgeAssignments
            .Include(assignment => assignment.Badge)
            .FirstOrDefaultAsync(
                assignment => assignment.ProductId == productId &&
                              assignment.BadgeId == badgeId &&
                              assignment.ProductVariantId == command.ProductVariantId,
                cancellationToken);

        if (existing is not null &&
            (existing.EndsAt is null || existing.EndsAt > DateTime.UtcNow))
        {
            return ServiceResult<ProductBadgeAssignment>.Fail(
                409,
                "This badge is already assigned to the selected product or variant.");
        }

        if (existing is not null)
        {
            // Reuse an expired manual assignment instead of accumulating duplicate rows.
            existing.StartsAt = startsAt;
            existing.EndsAt = endsAt;

            await dbContext.SaveChangesAsync(cancellationToken);
            InvalidateCatalog();

            return ServiceResult<ProductBadgeAssignment>.Ok(existing);
        }

        var entity = new ProductBadgeAssignment
        {
            ProductId = productId,
            ProductVariantId = command.ProductVariantId,
            BadgeId = badgeId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ProductBadgeAssignments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        return ServiceResult<ProductBadgeAssignment>.Ok(entity);
    }

    public async Task<ServiceResult> RemoveAssignmentAsync(
        int productId,
        long assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || assignmentId <= 0)
        {
            return ServiceResult.Fail(400, "Valid product and assignment IDs are required.");
        }

        var assignment = await dbContext.ProductBadgeAssignments
            .Include(item => item.Badge)
            .FirstOrDefaultAsync(
                item => item.Id == assignmentId && item.ProductId == productId,
                cancellationToken);

        if (assignment is null)
        {
            return ServiceResult.Fail(404, "Badge assignment was not found.");
        }

        if (string.Equals(
                assignment.Badge.AssignmentType,
                ProductBadgeAssignmentTypes.Automatic,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail(
                409,
                "Automatic badge assignments are managed by the system.");
        }

        dbContext.ProductBadgeAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCatalog();

        return ServiceResult.Ok();
    }

    public Task<List<ProductBadgeAssignment>> GetAssignmentsAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ProductBadgeAssignments
            .AsNoTracking()
            .Include(assignment => assignment.Badge)
            .Where(assignment => assignment.ProductId == productId)
            .OrderBy(assignment => assignment.Badge.SortOrder)
            .ThenBy(assignment => assignment.BadgeId)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);
    }

    private static string? ValidateSettings(
        string assignmentType,
        BadgeSettingsInput? settings)
    {
        if (settings is null)
        {
            return null;
        }

        if (string.Equals(
                assignmentType,
                ProductBadgeAssignmentTypes.Manual,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Manual badges do not use automatic settings.";
        }

        if (settings.PeriodDays is <= 0)
        {
            return "PeriodDays must be greater than zero.";
        }

        if (settings.Threshold is < 0)
        {
            return "Threshold cannot be negative.";
        }

        if (settings.DurationDays is <= 0)
        {
            return "DurationDays must be greater than zero.";
        }

        return null;
    }

    private static string? ValidateAutomaticSettings(
        string badgeCode,
        BadgeSettingsInput settings)
    {
        if (settings.PeriodDays is <= 0)
        {
            return "PeriodDays must be greater than zero.";
        }

        if (settings.Threshold is < 0)
        {
            return "Threshold cannot be negative.";
        }

        if (settings.DurationDays is <= 0)
        {
            return "DurationDays must be greater than zero.";
        }

        if (string.Equals(badgeCode, ProductBadgeCodes.Hit, StringComparison.OrdinalIgnoreCase))
        {
            if (settings.PeriodDays is null)
            {
                return "HIT requires PeriodDays.";
            }

            if (settings.Threshold is null)
            {
                return "HIT requires Threshold.";
            }
        }

        if (string.Equals(badgeCode, ProductBadgeCodes.New, StringComparison.OrdinalIgnoreCase) &&
            settings.PeriodDays is null)
        {
            return "NEW requires PeriodDays.";
        }

        if (string.Equals(badgeCode, ProductBadgeCodes.LowStock, StringComparison.OrdinalIgnoreCase) &&
            settings.Threshold is null)
        {
            return "LOW_STOCK requires Threshold.";
        }

        return null;
    }

    private void InvalidateCatalog()
    {
        cache.RemoveByPrefix(CacheKeys.ProductsPrefix);
        cache.RemoveByPrefix(CacheKeys.CatalogPrefix);
    }
}
