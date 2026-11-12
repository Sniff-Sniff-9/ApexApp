namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Способы сортировки списка активностей.
/// </summary>
public enum ActivitySortBy
{
    /// <summary>По количеству участников.</summary>
    Participants = 0,

    /// <summary>По доле завершения.</summary>
    CompletionRate = 1,

    /// <summary>По начисленным баллам.</summary>
    RewardPoints = 2,

    /// <summary>По дате начала (сначала новые).</summary>
    StartDate = 3
}

/// <summary>
/// Строка списка активностей одного формата.
/// </summary>
/// <param name="Format">Формат: <c>event</c>, <c>quest</c> или <c>survey</c>.</param>
/// <param name="Id">Идентификатор активности.</param>
/// <param name="Title">Название.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="StartsAt">Начало.</param>
/// <param name="EndsAt">Окончание.</param>
/// <param name="RewardAmount">Размер награды в баллах.</param>
/// <param name="Participants">Количество уникальных участников.</param>
/// <param name="Completed">Количество завершений (для мероприятий — посещений, для опросов — ответов).</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="Registered">Зарегистрировались (мероприятия).</param>
/// <param name="Attended">Посетили (мероприятия).</param>
/// <param name="NoShow">Не явились (мероприятия).</param>
/// <param name="Responses">Ответы (опросы).</param>
/// <param name="PendingProofs">Доказательства на проверке (задания).</param>
/// <param name="UnclaimedRewards">Незабранные награды.</param>
/// <param name="PointsAwarded">Начисленные баллы.</param>
public sealed record ActivityRow(
    string Format,
    long Id,
    string Title,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    long RewardAmount,
    long Participants,
    long Completed,
    double? CompletionRate,
    long? Registered,
    long? Attended,
    long? NoShow,
    long? Responses,
    long? PendingProofs,
    long? UnclaimedRewards,
    long PointsAwarded);

/// <summary>
/// Результат списка активностей.
/// </summary>
/// <param name="Items">Активности в порядке сортировки.</param>
/// <param name="TotalCount">Общее количество активностей до ограничения «топ N».</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record ActivityAnalyticsResult(
    IReadOnlyList<ActivityRow> Items,
    int TotalCount,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Шаг воронки активности.
/// </summary>
/// <param name="Step">Машинный код шага.</param>
/// <param name="Label">Название шага.</param>
/// <param name="Count">Количество участников на шаге.</param>
/// <param name="FromPrevious">Конверсия из предыдущего шага.</param>
/// <param name="FromStart">Конверсия из первого шага.</param>
public sealed record ActivityFunnelStep(
    string Step,
    string Label,
    long Count,
    double? FromPrevious,
    double? FromStart);

/// <summary>
/// Прохождение шага задания: сколько участников дошло до шага.
/// </summary>
/// <param name="StepId">Идентификатор шага.</param>
/// <param name="Title">Название шага.</param>
/// <param name="SortOrder">Порядок шага.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="Reached">Количество участников, выполнивших шаг.</param>
/// <param name="ConversionFromStart">Доля от числа начавших задание.</param>
public sealed record ActivityStepDropOff(
    long StepId,
    string Title,
    int SortOrder,
    bool IsRequired,
    long Reached,
    double? ConversionFromStart);

/// <summary>
/// Детальная аналитика одной активности.
/// </summary>
/// <param name="Format">Формат активности.</param>
/// <param name="Id">Идентификатор.</param>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="StartsAt">Начало.</param>
/// <param name="EndsAt">Окончание.</param>
/// <param name="RewardAmount">Награда в баллах.</param>
/// <param name="Metrics">Ключевые показатели.</param>
/// <param name="Funnel">Воронка участия.</param>
/// <param name="Steps">Прохождение шагов задания.</param>
/// <param name="Series">Ряд активности по корзинам.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record ActivityDetail(
    string Format,
    long Id,
    string Title,
    string? Description,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    long RewardAmount,
    IReadOnlyList<MetricValue> Metrics,
    IReadOnlyList<ActivityFunnelStep> Funnel,
    IReadOnlyList<ActivityStepDropOff> Steps,
    IReadOnlyList<AnalyticsSeriesPoint> Series,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Аналитика активностей: сравнение форматов, список активностей и детали одной активности.
/// </summary>
public interface IActivityAnalyticsService
{
    /// <summary>
    /// Возвращает список активностей, отсортированный по выбранному показателю.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="format">Формат или <c>null</c> для всех форматов.</param>
    /// <param name="sortBy">Способ сортировки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список активностей или ошибка 400.</returns>
    Task<ServiceResult<ActivityAnalyticsResult>> GetActivitiesAsync(
        AnalyticsQuery query,
        string? format,
        ActivitySortBy sortBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает детальную аналитику одной активности.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="format">Формат активности.</param>
    /// <param name="id">Идентификатор активности.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Детали активности или ошибка.</returns>
    Task<ServiceResult<ActivityDetail>> GetDetailAsync(
        AnalyticsQuery query,
        string format,
        long id,
        CancellationToken cancellationToken = default);
}

