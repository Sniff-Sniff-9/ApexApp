namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Строка агрегата временного ряда, возвращаемая PostgreSQL.
/// </summary>
internal sealed class SeriesBucketRow
{
    /// <summary>Начало корзины (UTC).</summary>
    public DateTime Bucket { get; set; }

    /// <summary>Значение показателя.</summary>
    public long Value { get; set; }

    /// <summary>Количество уникальных сотрудников.</summary>
    public long Users { get; set; }
}

/// <summary>
/// Скалярный агрегат, возвращаемый PostgreSQL.
/// </summary>
internal sealed class ScalarDoubleRow
{
    /// <summary>Значение агрегата.</summary>
    public double? Value { get; set; }
}
