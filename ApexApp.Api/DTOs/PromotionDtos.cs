namespace ApexApp.Api.DTOs;

/// <summary>Create/update payload for a product discount.</summary>
public sealed record UpsertProductDiscountRequest(
    int? ProductVariantId,
    int DiscountPercent,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive = true);

/// <summary>Discount returned to an administrator.</summary>
public sealed record ProductDiscountDto(
    long Id,
    int ProductId,
    int? ProductVariantId,
    int DiscountPercent,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Create payload for a badge definition.</summary>
public sealed record CreateProductBadgeRequest(
    string Code,
    string Name,
    string Color,
    string AssignmentType,
    bool IsActive,
    int SortOrder,
    BadgeSettingsRequest? Settings);

/// <summary>Update payload for a badge definition.</summary>
public sealed record UpdateProductBadgeRequest(
    string Name,
    string Color,
    string AssignmentType,
    bool IsActive,
    int SortOrder);

/// <summary>Automatic badge settings.</summary>
public sealed record BadgeSettingsRequest(
    int? PeriodDays,
    long? Threshold,
    int? DurationDays,
    bool IsEnabled);

/// <summary>Badge definition returned to an administrator.</summary>
public sealed record ProductBadgeDto(
    int Id,
    string Code,
    string Name,
    string Color,
    string AssignmentType,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    BadgeSettingsDto? Settings);

/// <summary>Badge settings returned to an administrator.</summary>
public sealed record BadgeSettingsDto(
    int BadgeId,
    int? PeriodDays,
    long? Threshold,
    int? DurationDays,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Manual badge assignment payload.</summary>
public sealed record AssignProductBadgeRequest(
    int BadgeId,
    int? ProductVariantId,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>Badge assignment returned to an administrator.</summary>
public sealed record ProductBadgeAssignmentDto(
    long Id,
    int ProductId,
    int? ProductVariantId,
    int BadgeId,
    string BadgeCode,
    string BadgeName,
    string BadgeColor,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateTime CreatedAt);
