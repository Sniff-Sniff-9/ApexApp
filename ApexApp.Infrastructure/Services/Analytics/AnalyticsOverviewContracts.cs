namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Сравнение одного формата активности с другими форматами.
/// </summary>
/// <param name="Format">Машинный код формата: <c>event</c>, <c>quest</c>, <c>survey</c>.</param>
/// <param name="Title">Название формата для отображения.</param>
/// <param name="Activities">Количество активностей формата, существовавших в окне.</param>
/// <param name="Participants">Количество уникальных участников.</param>
/// <param name="Actions">Количество учтённых действий.</param>
/// <param name="ReachRate">Доля сотрудников, участвовавших хотя бы раз.</param>
/// <param name="CompletionRate">Доля завершивших от начавших (для опросов — доля ответивших).</param>
/// <param name="RepeatRate">Доля участников с двумя и более действиями.</param>
/// <param name="RewardClaimRate">Доля полученных наград; <c>null</c>, если награда начисляется автоматически.</param>
/// <param name="PointsAwarded">Начисленные баллы за формат.</param>
/// <param name="IsReliable">Признак достаточного размера выборки.</param>
public sealed record ActivityFormatRow(
    string Format,
    string Title,
    long Activities,
    long Participants,
    long Actions,
    double? ReachRate,
    double? CompletionRate,
    double? RepeatRate,
    double? RewardClaimRate,
    long PointsAwarded,
    bool IsReliable);

/// <summary>
/// Обзор платформы: ключевые показатели, сравнение с предыдущим окном и форматы активности.
/// </summary>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Headcount">Численность и состав сотрудников.</param>
/// <param name="Participation">Участие сотрудников.</param>
/// <param name="Volume">Объём активности по форматам.</param>
/// <param name="Economy">Баллы и заказы.</param>
/// <param name="Deltas">Изменения относительно предыдущего окна.</param>
/// <param name="Formats">Сравнение форматов активности.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record PlatformOverview(
    AnalyticsRange Range,
    IReadOnlyList<MetricValue> Headcount,
    IReadOnlyList<MetricValue> Participation,
    IReadOnlyList<MetricValue> Volume,
    IReadOnlyList<MetricValue> Economy,
    IReadOnlyList<MetricDelta> Deltas,
    IReadOnlyList<ActivityFormatRow> Formats,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Сервис обзора платформы.
/// </summary>
public interface IAnalyticsOverviewService
{
    /// <summary>
    /// Возвращает обзор платформы за окно. Ограничение по отделу должно быть уже
    /// проверено вызывающим кодом.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обзор или ошибка 400.</returns>
    Task<ServiceResult<PlatformOverview>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
