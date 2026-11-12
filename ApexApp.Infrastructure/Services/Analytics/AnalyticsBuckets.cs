namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Построение и выравнивание временных корзин. Границы корзин считаются в настроенном
/// часовом поясе и возвращаются как моменты UTC, поэтому ряд не зависит от часового
/// пояса сервера приложения.
/// </summary>
public static class AnalyticsBuckets
{
    /// <summary>
    /// Строит непрерывный список корзин, покрывающих окно <c>[range.From, range.To)</c>.
    /// Первая корзина начинается не позже начала окна, последняя содержит момент конца окна.
    /// </summary>
    /// <param name="range">Окно расчёта.</param>
    /// <param name="maxBuckets">Максимальное количество корзин.</param>
    /// <returns>Упорядоченный список корзин.</returns>
    public static IReadOnlyList<AnalyticsBucket> Build(AnalyticsRange range, int maxBuckets)
    {
        var offset = TimeSpan.FromMinutes(range.TimeZoneOffsetMinutes);
        var cursor = Truncate(range.From, range.Granularity, offset);
        var buckets = new List<AnalyticsBucket>(capacity: Math.Max(maxBuckets, 1));

        while (true)
        {
            var next = Advance(cursor, range.Granularity);

            buckets.Add(new AnalyticsBucket(cursor, next));

            if (next >= range.To || buckets.Count >= Math.Max(maxBuckets, 1))
            {
                break;
            }

            cursor = next;
        }

        return buckets;
    }

    /// <summary>
    /// Усекает момент UTC до начала корзины в заданном смещении.
    /// </summary>
    /// <param name="utc">Момент UTC.</param>
    /// <param name="granularity">Гранулярность.</param>
    /// <param name="offset">Смещение часового пояса.</param>
    /// <returns>Начало корзины в UTC.</returns>
    public static DateTime Truncate(DateTime utc, AnalyticsGranularity granularity, TimeSpan offset)
    {
        var local = utc + offset;

        var start = granularity switch
        {
            AnalyticsGranularity.Month => new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            AnalyticsGranularity.Week => StartOfWeek(local),
            _ => local.Date
        };

        return DateTime.SpecifyKind(start - offset, DateTimeKind.Utc);
    }

    /// <summary>
    /// Возвращает начало следующей корзины.
    /// </summary>
    /// <param name="start">Начало текущей корзины (UTC).</param>
    /// <param name="granularity">Гранулярность.</param>
    /// <returns>Начало следующей корзины (UTC).</returns>
    public static DateTime Advance(DateTime start, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Month => start.AddMonths(1),
        AnalyticsGranularity.Week => start.AddDays(7),
        _ => start.AddDays(1)
    };

    /// <summary>
    /// Находит индекс корзины, содержащей момент.
    /// </summary>
    /// <param name="buckets">Список корзин.</param>
    /// <param name="momentUtc">Момент UTC.</param>
    /// <returns>Индекс корзины или <c>-1</c>, если момент вне ряда.</returns>
    public static int IndexOf(IReadOnlyList<AnalyticsBucket> buckets, DateTime momentUtc)
    {
        for (var index = 0; index < buckets.Count; index++)
        {
            if (momentUtc >= buckets[index].Start && momentUtc < buckets[index].End)
            {
                return index;
            }
        }

        return -1;
    }

    private static DateTime StartOfWeek(DateTime local)
    {
        var day = local.Date;

        // ISO-неделя начинается в понедельник; DayOfWeek использует воскресенье как 0.
        var delta = ((int)day.DayOfWeek + 6) % 7;

        return day.AddDays(-delta);
    }
}
