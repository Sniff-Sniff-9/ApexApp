using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Api.DTOs.Analytics;

/// <summary>
/// Параметры аналитического запроса.
/// </summary>
public sealed class AnalyticsQueryRequest
{
    /// <summary>Начало окна (UTC). По умолчанию — 30 дней назад.</summary>
    public DateTime? From { get; set; }

    /// <summary>Конец окна (UTC). По умолчанию — текущий момент.</summary>
    public DateTime? To { get; set; }

    /// <summary>Гранулярность рядов: <c>day</c>, <c>week</c> или <c>month</c>.</summary>
    public string? Granularity { get; set; }

    /// <summary>Ограничение по отделу. Руководителю доступен только свой отдел.</summary>
    public int? DepartmentId { get; set; }

    /// <summary>Размер списков «топ N».</summary>
    public int Top { get; set; } = 10;
}

/// <summary>
/// Проверенное окно расчёта.
/// </summary>
/// <param name="From">Начало текущего окна (UTC).</param>
/// <param name="To">Конец текущего окна (UTC).</param>
/// <param name="PreviousFrom">Начало предыдущего окна (UTC).</param>
/// <param name="PreviousTo">Конец предыдущего окна (UTC).</param>
/// <param name="Granularity">Гранулярность рядов.</param>
/// <param name="AsOf">Момент расчёта (UTC).</param>
/// <param name="TimeZone">Часовой пояс границ корзин.</param>
/// <param name="Caveats">Ограничения интерпретации данных.</param>
public sealed record AnalyticsRangeDto(
    DateTime From,
    DateTime To,
    DateTime PreviousFrom,
    DateTime PreviousTo,
    string Granularity,
    DateTime AsOf,
    string TimeZone,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="range">Окно расчёта.</param>
    /// <returns>DTO окна.</returns>
    public static AnalyticsRangeDto Create(AnalyticsRange range) => new(
        range.From,
        range.To,
        range.PreviousFrom,
        range.PreviousTo,
        range.GranularityCode,
        range.AsOf,
        range.TimeZone,
        range.Caveats);
}

/// <summary>
/// Показатель с признаком достоверности.
/// </summary>
/// <param name="Code">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Numerator">Числитель.</param>
/// <param name="Denominator">Знаменатель для долей.</param>
/// <param name="Value">Значение; <c>null</c>, если показатель недостоверен.</param>
/// <param name="Unit">Единица измерения.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
/// <param name="UnavailableReason">Причина отсутствия значения.</param>
public sealed record MetricDto(
    string Code,
    string Title,
    long? Numerator,
    long? Denominator,
    double? Value,
    string Unit,
    bool IsReliable,
    string? UnavailableReason)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="metric">Показатель.</param>
    /// <returns>DTO показателя.</returns>
    public static MetricDto From(MetricValue metric) => new(
        metric.Code,
        metric.Title,
        metric.Numerator,
        metric.Denominator,
        metric.Value,
        metric.Unit,
        metric.IsReliable,
        metric.UnavailableReason);
}

/// <summary>
/// Изменение показателя к предыдущему окну.
/// </summary>
/// <param name="Code">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Current">Значение в текущем окне.</param>
/// <param name="Previous">Значение в предыдущем окне.</param>
/// <param name="ChangePercent">Относительное изменение.</param>
/// <param name="Direction">Направление изменения.</param>
public sealed record MetricDeltaDto(
    string Code,
    string Title,
    double? Current,
    double? Previous,
    double? ChangePercent,
    string Direction)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="delta">Изменение.</param>
    /// <returns>DTO изменения.</returns>
    public static MetricDeltaDto From(MetricDelta delta) => new(
        delta.Code,
        delta.Title,
        delta.Current,
        delta.Previous,
        delta.ChangePercent,
        delta.Direction);
}

/// <summary>
/// Точка временного ряда.
/// </summary>
/// <param name="PeriodStart">Начало корзины (UTC).</param>
/// <param name="PeriodEnd">Конец корзины (UTC).</param>
/// <param name="Value">Значение показателя.</param>
/// <param name="UniqueUsers">Количество уникальных сотрудников.</param>
/// <param name="PerEmployee">Значение на сотрудника.</param>
/// <param name="ChangePercent">Изменение к предыдущей корзине.</param>
public sealed record SeriesPointDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    long Value,
    long UniqueUsers,
    double? PerEmployee,
    double? ChangePercent)
{
    /// <summary>Создаёт DTO из точки ряда.</summary>
    /// <param name="point">Точка ряда.</param>
    /// <returns>DTO точки.</returns>
    public static SeriesPointDto From(AnalyticsSeriesPoint point) => new(
        point.PeriodStart,
        point.PeriodEnd,
        point.Value,
        point.UniqueUsers,
        point.PerEmployee,
        point.ChangePercent);
}

/// <summary>
/// Временной ряд показателя.
/// </summary>
/// <param name="Metric">Машинный код показателя.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Unit">Единица измерения.</param>
/// <param name="Points">Точки ряда.</param>
public sealed record SeriesDto(
    string Metric,
    string Title,
    string Unit,
    IReadOnlyList<SeriesPointDto> Points)
{
    /// <summary>Создаёт DTO из ряда.</summary>
    /// <param name="series">Ряд.</param>
    /// <returns>DTO ряда.</returns>
    public static SeriesDto From(AnalyticsSeries series) => new(
        series.Metric,
        series.Title,
        series.Unit,
        series.Points.Select(SeriesPointDto.From).ToList());
}

/// <summary>
/// Разбирает параметры запроса в проверенную модель.
/// </summary>
public static class AnalyticsQueryBinder
{
    /// <summary>
    /// Создаёт модель запроса, проверяя гранулярность.
    /// </summary>
    /// <param name="request">Параметры запроса.</param>
    /// <param name="departmentId">Проверенный фильтр отдела.</param>
    /// <param name="query">Модель запроса.</param>
    /// <param name="error">Сообщение об ошибке.</param>
    /// <returns><c>true</c>, если параметры корректны.</returns>
    public static bool TryCreate(
        AnalyticsQueryRequest request,
        int? departmentId,
        out AnalyticsQuery query,
        out string? error)
    {
        if (!AnalyticsGranularityCodes.TryParse(request.Granularity, out var granularity))
        {
            query = new AnalyticsQuery();
            error = "Granularity must be one of: day, week, month.";
            return false;
        }

        query = new AnalyticsQuery(
            request.From,
            request.To,
            granularity,
            departmentId,
            request.Top <= 0 ? 10 : request.Top);

        error = null;
        return true;
    }
}

