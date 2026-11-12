namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Сравнение одного отдела с остальными. Все абсолютные значения сопровождаются
/// нормализованными показателями, потому что численность отделов различается.
/// </summary>
/// <param name="DepartmentId">Идентификатор отдела; <c>null</c> для сотрудников без отдела.</param>
/// <param name="Department">Название отдела.</param>
/// <param name="Headcount">Активные сотрудники отдела.</param>
/// <param name="ActiveEmployees">Сотрудники с активностью в окне.</param>
/// <param name="ActivityRate">Доля сотрудников с активностью.</param>
/// <param name="ActionsPerEmployee">Действий на активного сотрудника.</param>
/// <param name="EventParticipationPer100">Участников мероприятий на 100 сотрудников.</param>
/// <param name="QuestParticipationPer100">Участников заданий на 100 сотрудников.</param>
/// <param name="QuestCompletionRate">Доля завершённых заданий от начатых.</param>
/// <param name="SurveyParticipationPer100">Респондентов на 100 сотрудников.</param>
/// <param name="ThanksSentPerEmployee">Отправленных благодарностей на сотрудника.</param>
/// <param name="ThanksReceivedPerEmployee">Полученных благодарностей на сотрудника.</param>
/// <param name="CrossDepartmentThanksShare">Доля благодарностей сотрудникам других отделов.</param>
/// <param name="RewardUnitsPerEmployee">Единиц товара на сотрудника.</param>
/// <param name="RewardPointsPerEmployee">Списано баллов на сотрудника.</param>
/// <param name="EngagementScore">Средний балл вовлечённости по активным сотрудникам.</param>
/// <param name="EngagementChange">Изменение среднего балла к предыдущему периоду.</param>
/// <param name="Rank">Место по доле активности; <c>null</c> при недостаточной выборке.</param>
/// <param name="RankChange">Изменение места (положительное — улучшение).</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record DepartmentRow(
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
    bool IsReliable);

/// <summary>
/// Результат сравнения отделов.
/// </summary>
/// <param name="Rows">Отделы в порядке убывания места.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DepartmentAnalyticsResult(
    IReadOnlyList<DepartmentRow> Rows,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Детализация одного отдела.
/// </summary>
/// <param name="Row">Строка сравнения отдела.</param>
/// <param name="Series">Ряд участников отдела.</param>
/// <param name="Formats">Сравнение форматов внутри отдела.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DepartmentDetailResult(
    DepartmentRow Row,
    AnalyticsSeries Series,
    IReadOnlyList<ActivityFormatRow> Formats,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Аналитика отделов.
/// </summary>
public interface IDepartmentAnalyticsService
{
    /// <summary>Сравнение всех доступных отделов.</summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сравнение отделов или ошибка 400.</returns>
    Task<ServiceResult<DepartmentAnalyticsResult>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Детализация одного отдела.</summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="departmentId">Идентификатор отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Детализация или ошибка.</returns>
    Task<ServiceResult<DepartmentDetailResult>> GetDetailAsync(
        AnalyticsQuery query,
        int departmentId,
        CancellationToken cancellationToken = default);
}
