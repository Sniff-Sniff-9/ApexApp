namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Безопасные арифметические операции для аналитических показателей.
/// Все деления защищены от нулевого знаменателя и переполнения.
/// </summary>
public static class AnalyticsMetrics
{
    /// <summary>Минимальное абсолютное изменение, которое считается «не плоским» (в процентах).</summary>
    public const double FlatEpsilonPercent = 0.5d;

    /// <summary>
    /// Отношение числителя к знаменателю или <c>null</c>, если знаменатель нулевой.
    /// </summary>
    /// <param name="numerator">Числитель.</param>
    /// <param name="denominator">Знаменатель.</param>
    /// <returns>Доля или <c>null</c>.</returns>
    public static double? Ratio(long numerator, long denominator) =>
        denominator <= 0 ? null : (double)numerator / denominator;

    /// <summary>
    /// Количество на сто сотрудников — нормализованная метрика для сравнения отделов.
    /// </summary>
    /// <param name="count">Количество действий.</param>
    /// <param name="population">Численность.</param>
    /// <returns>Значение на 100 сотрудников или <c>null</c>.</returns>
    public static double? Per100(long count, long population) =>
        population <= 0 ? null : (double)count * 100d / population;

    /// <summary>
    /// Относительное изменение в процентах. Если база сравнения нулевая,
    /// изменение не вычисляется (нельзя делить на ноль и нельзя показать «бесконечность»).
    /// </summary>
    /// <param name="current">Текущее значение.</param>
    /// <param name="previous">Предыдущее значение.</param>
    /// <returns>Изменение в процентах или <c>null</c>.</returns>
    public static double? ChangePercent(double? current, double? previous)
    {
        if (current is null || previous is null || previous.Value == 0d)
        {
            return null;
        }

        return (current.Value - previous.Value) / Math.Abs(previous.Value) * 100d;
    }

    /// <summary>
    /// Направление изменения по абсолютной величине процента.
    /// </summary>
    /// <param name="changePercent">Изменение в процентах.</param>
    /// <returns><c>up</c>, <c>down</c>, <c>flat</c> или <c>unknown</c>.</returns>
    public static string Direction(double? changePercent)
    {
        if (changePercent is null)
        {
            return "unknown";
        }

        if (changePercent.Value > FlatEpsilonPercent)
        {
            return "up";
        }

        return changePercent.Value < -FlatEpsilonPercent ? "down" : "flat";
    }

    /// <summary>Округляет значение до указанного количества знаков.</summary>
    /// <param name="value">Значение.</param>
    /// <param name="digits">Количество знаков после запятой.</param>
    /// <returns>Округлённое значение или <c>null</c>.</returns>
    public static double? Round(double? value, int digits = 1) =>
        value is null
            ? null
            : Math.Round(value.Value, digits, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Медиана набора значений. Возвращает <c>null</c> для пустого набора.
    /// </summary>
    /// <param name="values">Значения.</param>
    /// <returns>Медиана или <c>null</c>.</returns>
    public static double? Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();

        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;

        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2d
            : ordered[middle];
    }

    /// <summary>
    /// Перцентиль (0..1) по уже упорядоченному набору.
    /// </summary>
    /// <param name="sortedValues">Упорядоченные по возрастанию значения.</param>
    /// <param name="percentile">Перцентиль от 0 до 1.</param>
    /// <returns>Значение перцентиля или <c>null</c> для пустого набора.</returns>
    public static double? Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return null;
        }

        var clamped = Math.Clamp(percentile, 0d, 1d);
        var index = (int)Math.Round(clamped * (sortedValues.Count - 1), MidpointRounding.AwayFromZero);

        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }

    /// <summary>
    /// Безопасное суммирование с защитой от переполнения.
    /// </summary>
    /// <param name="values">Слагаемые.</param>
    /// <returns>Сумма; при переполнении — <see cref="long.MaxValue"/>.</returns>
    public static long SafeSum(IEnumerable<long> values)
    {
        long total = 0;

        foreach (var value in values)
        {
            try
            {
                total = checked(total + value);
            }
            catch (OverflowException)
            {
                return value > 0 ? long.MaxValue : long.MinValue;
            }
        }

        return total;
    }

    /// <summary>
    /// Приводит дату к UTC, не меняя момент времени.
    /// </summary>
    /// <param name="value">Дата.</param>
    /// <returns>Дата в UTC.</returns>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
