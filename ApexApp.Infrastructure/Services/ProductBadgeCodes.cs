namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Stable badge codes consumed by the automatic badge rules and the admin UI.
/// Badge definitions themselves are configuration data and are expected to exist
/// in <c>product_badges</c>; this class does not seed them.
/// </summary>
public static class ProductBadgeCodes
{
    public const string Hit = "HIT";
    public const string New = "NEW";
    public const string Limited = "LIMITED";
    public const string LowStock = "LOW_STOCK";
    public const string Sale = "SALE";

    /// <summary>Badges whose code and assignment type are owned by the application.</summary>
    public static readonly IReadOnlySet<string> SystemCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Hit,
            New,
            Limited,
            LowStock,
            Sale
        };
}

/// <summary>
/// Values stored in <c>product_badges.assignment_type</c>.
/// </summary>
public static class ProductBadgeAssignmentTypes
{
    public const string Manual = "manual";
    public const string Automatic = "automatic";

    public static bool IsValid(string? value) =>
        string.Equals(value, Manual, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Automatic, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
}
