namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Формирует устойчивые сегменты ключей кеша аналитики. Момент расчёта округляется
/// до пятиминутной корзины, чтобы повторные загрузки дашборда попадали в кеш,
/// а числа оставались согласованными внутри одной корзины.
/// </summary>
public static class AnalyticsCacheTags
{
    /// <summary>Длина корзины момента расчёта.</summary>
    public static readonly TimeSpan InstantBucketSize = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Сегмент фильтра по отделу.
    /// </summary>
    /// <param name="departmentId">Идентификатор отдела или <c>null</c>.</param>
    /// <returns><c>dept-{id}</c> или <c>all</c>.</returns>
    public static string Filter(int? departmentId) =>
        departmentId is int id ? $"dept-{id}" : "all";

    /// <summary>
    /// Сегмент момента расчёта, округлённого вниз до размера корзины.
    /// </summary>
    /// <param name="asOf">Момент расчёта (UTC).</param>
    /// <returns>Строковый сегмент.</returns>
    public static string InstantBucket(DateTime asOf)
    {
        var ticks = asOf.Ticks - asOf.Ticks % InstantBucketSize.Ticks;

        return new DateTime(ticks, DateTimeKind.Utc).ToString("yyyyMMddHHmm");
    }
}
