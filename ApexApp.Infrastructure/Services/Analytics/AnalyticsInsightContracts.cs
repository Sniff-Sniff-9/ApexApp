namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Наблюдение, сформулированное по данным платформы. Вывод содержит только факты
/// и предлагаемое действие; причинные утверждения не делаются.
/// </summary>
/// <param name="Code">Машинный код правила.</param>
/// <param name="Severity">Важность: <c>info</c>, <c>warning</c> или <c>critical</c>.</param>
/// <param name="Title">Краткий заголовок.</param>
/// <param name="Description">Описание наблюдения.</param>
/// <param name="Evidence">Числовые подтверждения.</param>
/// <param name="SuggestedAction">Что стоит сделать.</param>
/// <param name="EntityType">Тип сущности, к которой относится наблюдение.</param>
/// <param name="EntityId">Идентификатор сущности.</param>
public sealed record Insight(
    string Code,
    string Severity,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> Evidence,
    string SuggestedAction,
    string? EntityType,
    long? EntityId);

/// <summary>
/// Набор наблюдений за окно.
/// </summary>
/// <param name="Insights">Наблюдения в порядке важности.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="GeneratedAt">Момент формирования.</param>
public sealed record InsightReport(
    IReadOnlyList<Insight> Insights,
    AnalyticsRange Range,
    DateTime GeneratedAt);

/// <summary>
/// Формирует наблюдения для принятия решений на основе уже рассчитанных показателей.
/// </summary>
public interface IAnalyticsInsightService
{
    /// <summary>
    /// Возвращает наблюдения за окно.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Наблюдения или ошибка 400.</returns>
    Task<ServiceResult<InsightReport>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
