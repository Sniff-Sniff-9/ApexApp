namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Гранулярность временных рядов аналитики.
/// </summary>
public enum AnalyticsGranularity
{
    /// <summary>Сутки.</summary>
    Day = 0,

    /// <summary>Календарная неделя (начинается в понедельник).</summary>
    Week = 1,

    /// <summary>Календарный месяц.</summary>
    Month = 2
}

/// <summary>
/// Запрос аналитики, полученный от клиента. Даты необязательны: при их отсутствии
/// используется окно по умолчанию из <see cref="AnalyticsOptions"/>.
/// </summary>
/// <param name="From">Начало окна (включительно) или <c>null</c>.</param>
/// <param name="To">Конец окна (исключительно) или <c>null</c>.</param>
/// <param name="Granularity">Гранулярность рядов.</param>
/// <param name="DepartmentId">Ограничение по отделу или <c>null</c>.</param>
/// <param name="Top">Размер списков «топ N».</param>
public sealed record AnalyticsQuery(
    DateTime? From = null,
    DateTime? To = null,
    AnalyticsGranularity Granularity = AnalyticsGranularity.Day,
    int? DepartmentId = null,
    int Top = 10);

/// <summary>
/// Нормализованное и проверенное окно расчёта. Окна задаются полуинтервалом
/// <c>[From, To)</c> в UTC, что исключает двойной учёт действий на границе.
/// </summary>
/// <param name="From">Начало текущего окна (включительно, UTC).</param>
/// <param name="To">Конец текущего окна (исключительно, UTC).</param>
/// <param name="PreviousFrom">Начало предыдущего окна той же длины (UTC).</param>
/// <param name="PreviousTo">Конец предыдущего окна той же длины (UTC).</param>
/// <param name="Granularity">Гранулярность рядов.</param>
/// <param name="AsOf">Момент расчёта, общий для всех запросов одного ответа (UTC).</param>
/// <param name="TimeZone">Часовой пояс границ корзин.</param>
/// <param name="TimeZoneOffsetMinutes">Смещение часового пояса в минутах на момент <see cref="AsOf"/>.</param>
/// <param name="Caveats">Ограничения интерпретации данных, действующие для всего ответа.</param>
public sealed record AnalyticsRange(
    DateTime From,
    DateTime To,
    DateTime PreviousFrom,
    DateTime PreviousTo,
    AnalyticsGranularity Granularity,
    DateTime AsOf,
    string TimeZone,
    int TimeZoneOffsetMinutes,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Длина текущего окна в днях.</summary>
    public double LengthDays => (To - From).TotalDays;

    /// <summary>Машинный код гранулярности.</summary>
    public string GranularityCode => AnalyticsGranularityCodes.Code(Granularity);

    /// <summary>Устойчивый тег окна для ключей кеша.</summary>
    public string CacheTag =>
        $"{From:yyyyMMddHH}-{To:yyyyMMddHH}-{GranularityCode}-{TimeZoneOffsetMinutes}";
}

/// <summary>
/// Машинные коды и разбор гранулярности.
/// </summary>
public static class AnalyticsGranularityCodes
{
    /// <summary>Сутки.</summary>
    public const string Day = "day";

    /// <summary>Неделя.</summary>
    public const string Week = "week";

    /// <summary>Месяц.</summary>
    public const string Month = "month";

    /// <summary>Возвращает машинный код гранулярности.</summary>
    /// <param name="granularity">Гранулярность.</param>
    /// <returns>Код <c>day</c>, <c>week</c> или <c>month</c>.</returns>
    public static string Code(AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Week => Week,
        AnalyticsGranularity.Month => Month,
        _ => Day
    };

    /// <summary>
    /// Разбирает строковый код гранулярности. Пустое значение трактуется как <c>day</c>.
    /// </summary>
    /// <param name="value">Строка запроса.</param>
    /// <param name="granularity">Разобранная гранулярность.</param>
    /// <returns><c>true</c>, если значение корректно.</returns>
    public static bool TryParse(string? value, out AnalyticsGranularity granularity)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or Day:
                granularity = AnalyticsGranularity.Day;
                return true;
            case Week:
                granularity = AnalyticsGranularity.Week;
                return true;
            case Month:
                granularity = AnalyticsGranularity.Month;
                return true;
            default:
                granularity = AnalyticsGranularity.Day;
                return false;
        }
    }
}

/// <summary>
/// Одна временная корзина ряда. Границы заданы в UTC.
/// </summary>
/// <param name="Start">Начало корзины (включительно).</param>
/// <param name="End">Конец корзины (исключительно).</param>
public sealed record AnalyticsBucket(DateTime Start, DateTime End);

/// <summary>
/// Показатель с числителем, знаменателем и признаком достоверности.
/// </summary>
/// <param name="Code">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Numerator">Числитель (или само значение для абсолютных показателей).</param>
/// <param name="Denominator">Знаменатель для долей; <c>null</c> для абсолютных показателей.</param>
/// <param name="Value">Рассчитанное значение; <c>null</c>, если показатель недостоверен или недоступен.</param>
/// <param name="Unit">Единица измерения: <c>count</c>, <c>rate</c>, <c>percent</c>, <c>points</c>, <c>hours</c>.</param>
/// <param name="IsReliable">Признак достаточного размера выборки.</param>
/// <param name="UnavailableReason">Причина отсутствия значения.</param>
public sealed record MetricValue(
    string Code,
    string Title,
    long? Numerator,
    long? Denominator,
    double? Value,
    string Unit,
    bool IsReliable,
    string? UnavailableReason)
{
    /// <summary>Создаёт абсолютный показатель.</summary>
    /// <param name="code">Машинный код.</param>
    /// <param name="title">Название.</param>
    /// <param name="value">Значение.</param>
    /// <param name="unit">Единица измерения.</param>
    /// <returns>Показатель с заполненным числителем.</returns>
    public static MetricValue Count(string code, string title, long value, string unit = "count") =>
        new(code, title, value, null, value, unit, true, null);

    /// <summary>
    /// Создаёт показатель-долю. Если знаменатель меньше минимальной выборки,
    /// значение не показывается, но числитель и знаменатель сохраняются.
    /// </summary>
    /// <param name="code">Машинный код.</param>
    /// <param name="title">Название.</param>
    /// <param name="numerator">Числитель.</param>
    /// <param name="denominator">Знаменатель.</param>
    /// <param name="minCohort">Минимальный размер выборки.</param>
    /// <returns>Показатель-доля.</returns>
    public static MetricValue Rate(string code, string title, long numerator, long denominator, int minCohort)
    {
        if (denominator <= 0)
        {
            return new MetricValue(code, title, numerator, denominator, null, "rate", false, "Нет знаменателя.");
        }

        var reliable = denominator >= minCohort;
        var value = reliable ? (double)numerator / denominator : (double?)null;

        return new MetricValue(
            code,
            title,
            numerator,
            denominator,
            value,
            "rate",
            reliable,
            reliable ? null : $"Выборка меньше {minCohort} наблюдений.");
    }

    /// <summary>Создаёт недоступный показатель с объяснением.</summary>
    /// <param name="code">Машинный код.</param>
    /// <param name="title">Название.</param>
    /// <param name="reason">Причина недоступности.</param>
    /// <returns>Показатель без значения.</returns>
    public static MetricValue Unavailable(string code, string title, string reason) =>
        new(code, title, null, null, null, "count", false, reason);
}

/// <summary>
/// Изменение показателя относительно предыдущего окна.
/// </summary>
/// <param name="Code">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Current">Значение в текущем окне.</param>
/// <param name="Previous">Значение в предыдущем окне.</param>
/// <param name="ChangePercent">Относительное изменение; <c>null</c>, если база сравнения нулевая.</param>
/// <param name="Direction">Направление: <c>up</c>, <c>down</c>, <c>flat</c> или <c>unknown</c>.</param>
public sealed record MetricDelta(
    string Code,
    string Title,
    double? Current,
    double? Previous,
    double? ChangePercent,
    string Direction);

/// <summary>
/// Точка временного ряда.
/// </summary>
/// <param name="PeriodStart">Начало корзины (UTC).</param>
/// <param name="PeriodEnd">Конец корзины (UTC).</param>
/// <param name="Value">Значение показателя в корзине.</param>
/// <param name="UniqueUsers">Количество уникальных сотрудников.</param>
/// <param name="PerEmployee">Значение на сотрудника; <c>null</c>, если база недостоверна.</param>
/// <param name="ChangePercent">Изменение относительно предыдущей корзины.</param>
public sealed record AnalyticsSeriesPoint(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    long Value,
    long UniqueUsers,
    double? PerEmployee,
    double? ChangePercent);

/// <summary>
/// Временной ряд одного показателя с заполненными нулями корзинами.
/// </summary>
/// <param name="Metric">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Unit">Единица измерения.</param>
/// <param name="Points">Точки ряда в порядке возрастания времени.</param>
public sealed record AnalyticsSeries(
    string Metric,
    string Title,
    string Unit,
    IReadOnlyList<AnalyticsSeriesPoint> Points);
