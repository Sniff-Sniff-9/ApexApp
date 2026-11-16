using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Проверяет и нормализует окно аналитики. Один вызов создаёт единый момент расчёта
/// <c>AsOf</c>, который затем используется всеми запросами ответа, чтобы все числа
/// были согласованы между собой.
/// </summary>
public interface IAnalyticsRangeFactory
{
    /// <summary>
    /// Создаёт нормализованное окно расчёта.
    /// </summary>
    /// <param name="query">Запрос клиента.</param>
    /// <returns>Окно расчёта или ошибка 400.</returns>
    ServiceResult<AnalyticsRange> Create(AnalyticsQuery query);
}

/// <summary>
/// Реализация <see cref="IAnalyticsRangeFactory"/> на основе <see cref="AnalyticsOptions"/>.
/// </summary>
public sealed class AnalyticsRangeFactory(IOptions<AnalyticsOptions> options) : IAnalyticsRangeFactory
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public ServiceResult<AnalyticsRange> Create(AnalyticsQuery query)
    {
        var asOf = DateTime.UtcNow;
        var to = query.To is DateTime requestedTo ? AnalyticsMetrics.ToUtc(requestedTo) : asOf;

        if (to > asOf.AddMinutes(1))
        {
            return ServiceResult<AnalyticsRange>.Fail(
                400,
                "The end of the range cannot be in the future.");
        }

        var from = query.From is DateTime requestedFrom
            ? AnalyticsMetrics.ToUtc(requestedFrom)
            : to.AddDays(-Math.Max(_options.DefaultRangeDays, 1));

        if (from >= to)
        {
            return ServiceResult<AnalyticsRange>.Fail(
                400,
                "The start of the range must be earlier than the end.");
        }

        var lengthDays = (to - from).TotalDays;

        if (lengthDays > _options.MaxRangeDays)
        {
            return ServiceResult<AnalyticsRange>.Fail(
                400,
                $"The range cannot exceed {_options.MaxRangeDays} days.");
        }

        var bucketCount = CountBuckets(from, to, query.Granularity, _options.TimeZone);

        if (bucketCount > _options.MaxBuckets)
        {
            return ServiceResult<AnalyticsRange>.Fail(
                400,
                $"The range produces {bucketCount} buckets, which exceeds the limit of {_options.MaxBuckets}. " +
                "Use a coarser granularity or a shorter range.");
        }

        var offset = ResolveOffset(_options.TimeZone, asOf);

        return ServiceResult<AnalyticsRange>.Ok(new AnalyticsRange(
            from,
            to,
            from.AddDays(-lengthDays),
            from,
            query.Granularity,
            asOf,
            _options.TimeZone,
            (int)offset.TotalMinutes));
    }

    /// <summary>
    /// Считает количество корзин, не строя их список.
    /// </summary>
    /// <param name="from">Начало окна.</param>
    /// <param name="to">Конец окна.</param>
    /// <param name="granularity">Гранулярность.</param>
    /// <param name="timeZone">Часовой пояс.</param>
    /// <returns>Количество корзин.</returns>
    internal static int CountBuckets(DateTime from, DateTime to, AnalyticsGranularity granularity, string timeZone)
    {
        var offset = ResolveOffset(timeZone, to);

        if (granularity == AnalyticsGranularity.Day)
        {
            return Math.Max(1, (int)Math.Ceiling((to - from).TotalDays) + 1);
        }

        if (granularity == AnalyticsGranularity.Week)
        {
            return Math.Max(1, (int)Math.Ceiling((to - from).TotalDays / 7d) + 1);
        }

        return Math.Max(1, (int)Math.Ceiling((to - from).TotalDays / 28d) + 1);
    }

    /// <summary>
    /// Возвращает смещение часового пояса на указанный момент.
    /// </summary>
    /// <param name="timeZone">Идентификатор часового пояса IANA или системный.</param>
    /// <param name="momentUtc">Момент UTC.</param>
    /// <returns>Смещение от UTC.</returns>
    internal static TimeSpan ResolveOffset(string timeZone, DateTime momentUtc)
    {
        if (string.IsNullOrWhiteSpace(timeZone)
            || string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.Zero;
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);

            return zone.GetUtcOffset(DateTime.SpecifyKind(momentUtc, DateTimeKind.Utc));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeSpan.Zero;
        }
    }
   
}
