using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Api.DTOs.Analytics;

/// <summary>
/// Сравнение одного формата активности.
/// </summary>
/// <param name="Format">Машинный код формата.</param>
/// <param name="Title">Название формата.</param>
/// <param name="Activities">Количество активностей.</param>
/// <param name="Participants">Уникальные участники.</param>
/// <param name="Actions">Количество действий.</param>
/// <param name="ReachRate">Доля охвата сотрудников.</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="RepeatRate">Доля повторных участников.</param>
/// <param name="RewardClaimRate">Доля полученных наград.</param>
/// <param name="PointsAwarded">Начисленные баллы.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record ActivityFormatDto(
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
    bool IsReliable)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка сравнения форматов.</param>
    /// <returns>DTO формата.</returns>
    public static ActivityFormatDto From(ActivityFormatRow row) => new(
        row.Format,
        row.Title,
        row.Activities,
        row.Participants,
        row.Actions,
        row.ReachRate,
        row.CompletionRate,
        row.RepeatRate,
        row.RewardClaimRate,
        row.PointsAwarded,
        row.IsReliable);
}

/// <summary>
/// Обзор платформы.
/// </summary>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Headcount">Численность и состав сотрудников.</param>
/// <param name="Participation">Участие сотрудников.</param>
/// <param name="Volume">Объём активности.</param>
/// <param name="Economy">Баллы и заказы.</param>
/// <param name="Deltas">Изменения к предыдущему окну.</param>
/// <param name="Formats">Сравнение форматов.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record PlatformOverviewDto(
    AnalyticsRangeDto Range,
    IReadOnlyList<MetricDto> Headcount,
    IReadOnlyList<MetricDto> Participation,
    IReadOnlyList<MetricDto> Volume,
    IReadOnlyList<MetricDto> Economy,
    IReadOnlyList<MetricDeltaDto> Deltas,
    IReadOnlyList<ActivityFormatDto> Formats,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="overview">Обзор платформы.</param>
    /// <returns>DTO обзора.</returns>
    public static PlatformOverviewDto From(PlatformOverview overview) => new(
        AnalyticsRangeDto.Create(overview.Range),
        overview.Headcount.Select(MetricDto.From).ToList(),
        overview.Participation.Select(MetricDto.From).ToList(),
        overview.Volume.Select(MetricDto.From).ToList(),
        overview.Economy.Select(MetricDto.From).ToList(),
        overview.Deltas.Select(MetricDeltaDto.From).ToList(),
        overview.Formats.Select(ActivityFormatDto.From).ToList(),
        overview.Caveats);
}

/// <summary>
/// Строка списка активностей.
/// </summary>
/// <param name="Format">Формат активности.</param>
/// <param name="Id">Идентификатор активности.</param>
/// <param name="Title">Название.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="StartsAt">Начало.</param>
/// <param name="EndsAt">Окончание.</param>
/// <param name="RewardAmount">Награда в баллах.</param>
/// <param name="Participants">Уникальные участники.</param>
/// <param name="Completed">Количество завершений.</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="Registered">Зарегистрировались.</param>
/// <param name="Attended">Посетили.</param>
/// <param name="NoShow">Не явились.</param>
/// <param name="Responses">Ответы на опрос.</param>
/// <param name="PendingProofs">Доказательства на проверке.</param>
/// <param name="UnclaimedRewards">Незабранные награды.</param>
/// <param name="PointsAwarded">Начисленные баллы.</param>
public sealed record ActivityListItemDto(
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
    long PointsAwarded)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка списка.</param>
    /// <returns>DTO строки.</returns>
    public static ActivityListItemDto From(ActivityRow row) => new(
        row.Format,
        row.Id,
        row.Title,
        row.IsActive,
        row.StartsAt,
        row.EndsAt,
        row.RewardAmount,
        row.Participants,
        row.Completed,
        row.CompletionRate,
        row.Registered,
        row.Attended,
        row.NoShow,
        row.Responses,
        row.PendingProofs,
        row.UnclaimedRewards,
        row.PointsAwarded);
}

/// <summary>
/// Список активностей.
/// </summary>
/// <param name="Items">Активности в порядке сортировки.</param>
/// <param name="TotalCount">Всего активностей до ограничения.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record ActivityListDto(
    IReadOnlyList<ActivityListItemDto> Items,
    int TotalCount,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Результат списка.</param>
    /// <returns>DTO списка.</returns>
    public static ActivityListDto From(ActivityAnalyticsResult result) => new(
        result.Items.Select(ActivityListItemDto.From).ToList(),
        result.TotalCount,
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

/// <summary>
/// Шаг воронки активности.
/// </summary>
/// <param name="Step">Машинный код шага.</param>
/// <param name="Label">Название шага.</param>
/// <param name="Count">Количество участников.</param>
/// <param name="FromPrevious">Конверсия из предыдущего шага.</param>
/// <param name="FromStart">Конверсия из первого шага.</param>
public sealed record FunnelStepDto(
    string Step,
    string Label,
    long Count,
    double? FromPrevious,
    double? FromStart)
{
    /// <summary>Создаёт DTO из шага воронки.</summary>
    /// <param name="step">Шаг.</param>
    /// <returns>DTO шага.</returns>
    public static FunnelStepDto From(ActivityFunnelStep step) => new(
        step.Step,
        step.Label,
        step.Count,
        step.FromPrevious,
        step.FromStart);
}

/// <summary>
/// Прохождение шага задания.
/// </summary>
/// <param name="StepId">Идентификатор шага.</param>
/// <param name="Title">Название шага.</param>
/// <param name="SortOrder">Порядок шага.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="Reached">Сколько участников выполнили шаг.</param>
/// <param name="ConversionFromStart">Доля от начавших задание.</param>
public sealed record StepDropOffDto(
    long StepId,
    string Title,
    int SortOrder,
    bool IsRequired,
    long Reached,
    double? ConversionFromStart)
{
    /// <summary>Создаёт DTO из шага задания.</summary>
    /// <param name="step">Шаг.</param>
    /// <returns>DTO шага.</returns>
    public static StepDropOffDto From(ActivityStepDropOff step) => new(
        step.StepId,
        step.Title,
        step.SortOrder,
        step.IsRequired,
        step.Reached,
        step.ConversionFromStart);
}

/// <summary>
/// Детальная аналитика активности.
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
/// <param name="Series">Ряд активности.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record ActivityDetailDto(
    string Format,
    long Id,
    string Title,
    string? Description,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    long RewardAmount,
    IReadOnlyList<MetricDto> Metrics,
    IReadOnlyList<FunnelStepDto> Funnel,
    IReadOnlyList<StepDropOffDto> Steps,
    IReadOnlyList<SeriesPointDto> Series,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="detail">Детали активности.</param>
    /// <returns>DTO деталей.</returns>
    public static ActivityDetailDto From(ActivityDetail detail) => new(
        detail.Format,
        detail.Id,
        detail.Title,
        detail.Description,
        detail.IsActive,
        detail.StartsAt,
        detail.EndsAt,
        detail.RewardAmount,
        detail.Metrics.Select(MetricDto.From).ToList(),
        detail.Funnel.Select(FunnelStepDto.From).ToList(),
        detail.Steps.Select(StepDropOffDto.From).ToList(),
        detail.Series.Select(SeriesPointDto.From).ToList(),
        detail.Caveats);
}

