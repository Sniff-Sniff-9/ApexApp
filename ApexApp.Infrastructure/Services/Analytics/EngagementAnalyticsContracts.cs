namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Распределение сотрудников по уровням вовлечённости.
/// </summary>
/// <param name="Code">Машинный код уровня.</param>
/// <param name="Title">Название уровня.</param>
/// <param name="Rank">Порядковый номер уровня.</param>
/// <param name="Count">Количество сотрудников уровня.</param>
public sealed record EngagementTierCount(
    string Code,
    string Title,
    int Rank,
    long Count);

/// <summary>
/// Точка тренда вовлечённости за период.
/// </summary>
/// <param name="PeriodStart">Начало периода (UTC).</param>
/// <param name="PeriodEnd">Конец периода (UTC).</param>
/// <param name="WindowStart">Начало 30-дневного окна расчёта (UTC).</param>
/// <param name="AverageScore">Средний балл по активным сотрудникам (без активности — ноль).</param>
/// <param name="MedianScore">Медианный балл по активным сотрудникам.</param>
/// <param name="ScoredEmployees">Количество сотрудников с активностью в окне.</param>
/// <param name="Headcount">Активные сотрудники на момент расчёта.</param>
/// <param name="TopTierCount">Количество сотрудников в двух старших уровнях.</param>
/// <param name="PreviousAverageScore">Средний балл предыдущего периода.</param>
/// <param name="ChangePoints">Изменение среднего балла в пунктах.</param>
/// <param name="Tiers">Распределение по уровням.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record EngagementTrendPoint(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime WindowStart,
    double? AverageScore,
    double? MedianScore,
    long ScoredEmployees,
    long Headcount,
    long TopTierCount,
    double? PreviousAverageScore,
    double? ChangePoints,
    IReadOnlyList<EngagementTierCount> Tiers,
    bool IsReliable);

/// <summary>
/// Тренд вовлечённости.
/// </summary>
/// <param name="Points">Точки тренда в порядке возрастания времени.</param>
/// <param name="WindowDays">Длина окна расчёта в днях.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record EngagementTrendResult(
    IReadOnlyList<EngagementTrendPoint> Points,
    int WindowDays,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Сотрудник со снижающейся вовлечённостью.
/// </summary>
/// <param name="EmployeeRef">Обезличенный устойчивый идентификатор сотрудника.</param>
/// <param name="Name">ФИО; <c>null</c>, если вызывающий код не имеет права видеть имена.</param>
/// <param name="DepartmentId">Текущий отдел или <c>null</c>.</param>
/// <param name="Department">Название отдела.</param>
/// <param name="CurrentScore">Текущий балл.</param>
/// <param name="PreviousScore">Балл предыдущего периода.</param>
/// <param name="Change">Изменение балла.</param>
/// <param name="Actions">Количество учтённых действий в текущем окне.</param>
/// <param name="LastActivityAt">Последнее учтённое действие.</param>
public sealed record DecliningEmployee(
    string EmployeeRef,
    string? Name,
    int? DepartmentId,
    string? Department,
    double CurrentScore,
    double PreviousScore,
    double Change,
    long Actions,
    DateTime? LastActivityAt);

/// <summary>
/// Список сотрудников со снижающейся вовлечённостью.
/// </summary>
/// <param name="Employees">Сотрудники в порядке наибольшего падения.</param>
/// <param name="TotalCount">Общее количество сотрудников, попавших в правило.</param>
/// <param name="SilentEmployees">Активные сотрудники без активности в окне.</param>
/// <param name="EvaluatedEmployees">Количество активных сотрудников, участвовавших в расчёте.</param>
/// <param name="Threshold">Порог падения в пунктах.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DecliningEmployeesResult(
    IReadOnlyList<DecliningEmployee> Employees,
    int TotalCount,
    long SilentEmployees,
    long EvaluatedEmployees,
    double Threshold,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Аналитика вовлечённости: исторический тренд и сотрудники со снижением активности.
/// </summary>
public interface IEngagementAnalyticsService
{
    /// <summary>
    /// Возвращает тренд вовлечённости. Расчёт повторно использует существующую
    /// формулу вовлечённости для каждого периода.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Тренд или ошибка 400.</returns>
    Task<ServiceResult<EngagementTrendResult>> GetTrendAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает сотрудников со снижающейся вовлечённостью.
    /// </summary>
    /// <param name="query">Запрос клиента с проверенным фильтром отдела.</param>
    /// <param name="includeNames">Признак права видеть имена сотрудников.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список или ошибка 400.</returns>
    Task<ServiceResult<DecliningEmployeesResult>> GetDecliningAsync(
        AnalyticsQuery query,
        bool includeNames,
        CancellationToken cancellationToken = default);
}
