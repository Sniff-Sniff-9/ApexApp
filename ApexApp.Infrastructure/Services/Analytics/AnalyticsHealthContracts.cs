namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Одна проверка качества данных или состояния платформы.
/// </summary>
/// <param name="Code">Машинный код проверки.</param>
/// <param name="Title">Название проверки.</param>
/// <param name="Severity">Важность: <c>info</c>, <c>warning</c> или <c>critical</c>.</param>
/// <param name="Value">Измеренное значение.</param>
/// <param name="Total">База для доли, если применимо.</param>
/// <param name="Description">Пояснение.</param>
public sealed record AnalyticsHealthCheck(
    string Code,
    string Title,
    string Severity,
    long Value,
    long? Total,
    string? Description);

/// <summary>
/// Состояние данных, влияющих на корректность аналитики.
/// </summary>
/// <param name="Checks">Проверки в порядке важности.</param>
/// <param name="AsOf">Момент расчёта (UTC).</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record AnalyticsHealth(
    IReadOnlyList<AnalyticsHealthCheck> Checks,
    DateTime AsOf,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Проверки качества данных аналитики.
/// </summary>
public interface IAnalyticsHealthService
{
    /// <summary>
    /// Возвращает результаты проверок.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние данных.</returns>
    Task<ServiceResult<AnalyticsHealth>> GetAsync(CancellationToken cancellationToken = default);
}
