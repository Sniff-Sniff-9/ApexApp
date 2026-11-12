using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Api.DTOs.Analytics;

/// <summary>
/// Строка сравнения отдела.
/// </summary>
/// <param name="DepartmentId">Идентификатор отдела.</param>
/// <param name="Department">Название отдела.</param>
/// <param name="Headcount">Активные сотрудники.</param>
/// <param name="ActiveEmployees">Сотрудники с активностью.</param>
/// <param name="ActivityRate">Доля участия.</param>
/// <param name="ActionsPerEmployee">Действий на активного сотрудника.</param>
/// <param name="EventParticipationPer100">Участников мероприятий на 100 сотрудников.</param>
/// <param name="QuestParticipationPer100">Участников заданий на 100 сотрудников.</param>
/// <param name="QuestCompletionRate">Доля завершённых заданий.</param>
/// <param name="SurveyParticipationPer100">Респондентов на 100 сотрудников.</param>
/// <param name="ThanksSentPerEmployee">Отправленных благодарностей на сотрудника.</param>
/// <param name="ThanksReceivedPerEmployee">Полученных благодарностей на сотрудника.</param>
/// <param name="CrossDepartmentThanksShare">Доля благодарностей другим отделам.</param>
/// <param name="RewardUnitsPerEmployee">Единиц товара на сотрудника.</param>
/// <param name="RewardPointsPerEmployee">Списано баллов на сотрудника.</param>
/// <param name="EngagementScore">Средний балл вовлечённости.</param>
/// <param name="EngagementChange">Изменение балла.</param>
/// <param name="Rank">Место по доле участия.</param>
/// <param name="RankChange">Изменение места.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record DepartmentRowDto(
    int? DepartmentId,
    string Department,
    long Headcount,
    long ActiveEmployees,
    double? ActivityRate,
    double? ActionsPerEmployee,
    double? EventParticipationPer100,
    double? QuestParticipationPer100,
    double? QuestCompletionRate,
    double? SurveyParticipationPer100,
    double? ThanksSentPerEmployee,
    double? ThanksReceivedPerEmployee,
    double? CrossDepartmentThanksShare,
    double? RewardUnitsPerEmployee,
    double? RewardPointsPerEmployee,
    double? EngagementScore,
    double? EngagementChange,
    int? Rank,
    int? RankChange,
    bool IsReliable)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка отдела.</param>
    /// <returns>DTO строки.</returns>
    public static DepartmentRowDto From(DepartmentRow row) => new(
        row.DepartmentId,
        row.Department,
        row.Headcount,
        row.ActiveEmployees,
        row.ActivityRate,
        row.ActionsPerEmployee,
        row.EventParticipationPer100,
        row.QuestParticipationPer100,
        row.QuestCompletionRate,
        row.SurveyParticipationPer100,
        row.ThanksSentPerEmployee,
        row.ThanksReceivedPerEmployee,
        row.CrossDepartmentThanksShare,
        row.RewardUnitsPerEmployee,
        row.RewardPointsPerEmployee,
        row.EngagementScore,
        row.EngagementChange,
        row.Rank,
        row.RankChange,
        row.IsReliable);
}

/// <summary>
/// Сравнение отделов.
/// </summary>
/// <param name="Rows">Отделы в порядке места.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DepartmentAnalyticsDto(
    IReadOnlyList<DepartmentRowDto> Rows,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Сравнение отделов.</param>
    /// <returns>DTO сравнения.</returns>
    public static DepartmentAnalyticsDto From(DepartmentAnalyticsResult result) => new(
        result.Rows.Select(DepartmentRowDto.From).ToList(),
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

/// <summary>
/// Детализация одного отдела.
/// </summary>
/// <param name="Row">Строка сравнения.</param>
/// <param name="Series">Ряд участников.</param>
/// <param name="Formats">Сравнение форматов внутри отдела.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DepartmentDetailDto(
    DepartmentRowDto Row,
    SeriesDto Series,
    IReadOnlyList<ActivityFormatDto> Formats,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="detail">Детализация отдела.</param>
    /// <returns>DTO детализации.</returns>
    public static DepartmentDetailDto From(DepartmentDetailResult detail) => new(
        DepartmentRowDto.From(detail.Row),
        SeriesDto.From(detail.Series),
        detail.Formats.Select(ActivityFormatDto.From).ToList(),
        AnalyticsRangeDto.Create(detail.Range),
        detail.Caveats);
}

/// <summary>
/// Пара отделов в матрице взаимодействия.
/// </summary>
/// <param name="FromDepartmentId">Отдел отправителя.</param>
/// <param name="FromDepartment">Название отдела отправителя.</param>
/// <param name="ToDepartmentId">Отдел получателя.</param>
/// <param name="ToDepartment">Название отдела получателя.</param>
/// <param name="Count">Количество благодарностей.</param>
/// <param name="IsCrossDepartment">Признак межотдельского взаимодействия.</param>
public sealed record DepartmentInteractionDto(
    int? FromDepartmentId,
    string FromDepartment,
    int? ToDepartmentId,
    string ToDepartment,
    long Count,
    bool IsCrossDepartment)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка пары отделов.</param>
    /// <returns>DTO пары.</returns>
    public static DepartmentInteractionDto From(DepartmentInteractionRow row) => new(
        row.FromDepartmentId,
        row.FromDepartment,
        row.ToDepartmentId,
        row.ToDepartment,
        row.Count,
        row.IsCrossDepartment);
}

/// <summary>
/// Точка ряда межотдельских благодарностей.
/// </summary>
/// <param name="PeriodStart">Начало корзины.</param>
/// <param name="PeriodEnd">Конец корзины.</param>
/// <param name="Total">Всего благодарностей.</param>
/// <param name="CrossDepartment">Межотдельские благодарности.</param>
/// <param name="CrossDepartmentShare">Доля межотдельских.</param>
public sealed record CrossDepartmentPointDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    long Total,
    long CrossDepartment,
    double? CrossDepartmentShare)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="point">Точка ряда.</param>
    /// <returns>DTO точки.</returns>
    public static CrossDepartmentPointDto From(CrossDepartmentSeriesPoint point) => new(
        point.PeriodStart,
        point.PeriodEnd,
        point.Total,
        point.CrossDepartment,
        point.CrossDepartmentShare);
}

/// <summary>
/// Взаимодействие отделов через благодарности.
/// </summary>
/// <param name="Total">Всего благодарностей.</param>
/// <param name="CrossDepartment">Межотдельские благодарности.</param>
/// <param name="CrossDepartmentShare">Доля межотдельских.</param>
/// <param name="PreviousCrossDepartmentShare">Доля в предыдущем окне.</param>
/// <param name="TopPairs">Наиболее частые пары отделов.</param>
/// <param name="Series">Ряд доли межотдельских благодарностей.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record CollaborationThanksDto(
    long Total,
    long CrossDepartment,
    double? CrossDepartmentShare,
    double? PreviousCrossDepartmentShare,
    IReadOnlyList<DepartmentInteractionDto> TopPairs,
    IReadOnlyList<CrossDepartmentPointDto> Series,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Результат взаимодействия.</param>
    /// <returns>DTO взаимодействия.</returns>
    public static CollaborationThanksDto From(CollaborationThanksResult result) => new(
        result.Total,
        result.CrossDepartment,
        result.CrossDepartmentShare,
        result.PreviousCrossDepartmentShare,
        result.TopPairs.Select(DepartmentInteractionDto.From).ToList(),
        result.Series.Select(CrossDepartmentPointDto.From).ToList(),
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

/// <summary>
/// Наблюдение по данным платформы.
/// </summary>
/// <param name="Code">Машинный код правила.</param>
/// <param name="Severity">Важность.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Description">Описание.</param>
/// <param name="Evidence">Числовые подтверждения.</param>
/// <param name="SuggestedAction">Предлагаемое действие.</param>
/// <param name="EntityType">Тип сущности.</param>
/// <param name="EntityId">Идентификатор сущности.</param>
public sealed record InsightDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> Evidence,
    string SuggestedAction,
    string? EntityType,
    long? EntityId)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="insight">Наблюдение.</param>
    /// <returns>DTO наблюдения.</returns>
    public static InsightDto From(Insight insight) => new(
        insight.Code,
        insight.Severity,
        insight.Title,
        insight.Description,
        insight.Evidence,
        insight.SuggestedAction,
        insight.EntityType,
        insight.EntityId);
}

/// <summary>
/// Набор наблюдений.
/// </summary>
/// <param name="Insights">Наблюдения в порядке важности.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="GeneratedAt">Момент формирования.</param>
public sealed record InsightReportDto(
    IReadOnlyList<InsightDto> Insights,
    AnalyticsRangeDto Range,
    DateTime GeneratedAt)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="report">Набор наблюдений.</param>
    /// <returns>DTO набора.</returns>
    public static InsightReportDto From(InsightReport report) => new(
        report.Insights.Select(InsightDto.From).ToList(),
        AnalyticsRangeDto.Create(report.Range),
        report.GeneratedAt);
}

/// <summary>
/// Проверка качества данных.
/// </summary>
/// <param name="Code">Машинный код проверки.</param>
/// <param name="Title">Название проверки.</param>
/// <param name="Severity">Важность.</param>
/// <param name="Value">Измеренное значение.</param>
/// <param name="Total">База для доли.</param>
/// <param name="Description">Пояснение.</param>
public sealed record HealthCheckDto(
    string Code,
    string Title,
    string Severity,
    long Value,
    long? Total,
    string? Description)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="check">Проверка.</param>
    /// <returns>DTO проверки.</returns>
    public static HealthCheckDto From(AnalyticsHealthCheck check) => new(
        check.Code,
        check.Title,
        check.Severity,
        check.Value,
        check.Total,
        check.Description);
}

/// <summary>
/// Состояние данных аналитики.
/// </summary>
/// <param name="Checks">Проверки в порядке важности.</param>
/// <param name="AsOf">Момент расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record AnalyticsHealthDto(
    IReadOnlyList<HealthCheckDto> Checks,
    DateTime AsOf,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="health">Состояние данных.</param>
    /// <returns>DTO состояния.</returns>
    public static AnalyticsHealthDto From(AnalyticsHealth health) => new(
        health.Checks.Select(HealthCheckDto.From).ToList(),
        health.AsOf,
        health.Caveats);
}

