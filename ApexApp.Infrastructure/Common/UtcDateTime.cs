namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Normalizes API values to UTC before they are written to PostgreSQL
/// <c>timestamp with time zone</c> columns.
/// </summary>
public static class UtcDateTime
{
    /// <summary>Normalizes a required timestamp.</summary>
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>Normalizes an optional timestamp.</summary>
    public static DateTime? Normalize(DateTime? value) =>
        value.HasValue ? Normalize(value.Value) : null;
}
