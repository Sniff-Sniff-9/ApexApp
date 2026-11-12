namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Справочные данные о сотрудниках и отделах, используемые всеми аналитическими сервисами.
/// </summary>
/// <param name="TotalUsers">Всего учётных записей.</param>
/// <param name="ActiveUsers">Активные учётные записи.</param>
/// <param name="InactiveUsers">Неактивные учётные записи.</param>
/// <param name="ActiveWithoutDepartment">Активные сотрудники без отдела.</param>
/// <param name="DepartmentIdByUser">Текущий отдел активных сотрудников.</param>
/// <param name="DepartmentNames">Названия отделов.</param>
/// <param name="ActiveByDepartment">Активные сотрудники по отделам.</param>
/// <param name="ActiveUserIds">Идентификаторы активных сотрудников.</param>
public sealed record AnalyticsPopulation(
    long TotalUsers,
    long ActiveUsers,
    long InactiveUsers,
    long ActiveWithoutDepartment,
    IReadOnlyDictionary<Guid, int?> DepartmentIdByUser,
    IReadOnlyDictionary<int, string> DepartmentNames,
    IReadOnlyDictionary<int, long> ActiveByDepartment,
    IReadOnlySet<Guid> ActiveUserIds);

/// <summary>
/// Показатели активности одного сотрудника за один период.
/// </summary>
/// <param name="EventsAttended">Посещённые мероприятия.</param>
/// <param name="QuestsJoined">Начатые задания.</param>
/// <param name="QuestsCompleted">Завершённые задания.</param>
/// <param name="QuestsCancelled">Отменённые задания.</param>
/// <param name="SurveysSubmitted">Отправленные ответы на опросы.</param>
/// <param name="DistinctSurveys">Количество разных опросов с ответами.</param>
/// <param name="ThanksSent">Отправленные благодарности.</param>
/// <param name="ThanksReceived">Полученные благодарности.</param>
/// <param name="Orders">Оформленные заказы.</param>
/// <param name="UnitsOrdered">Единиц товара в заказах.</param>
/// <param name="PointsEarned">Начисленные баллы за активности.</param>
/// <param name="PointsSpent">Потраченные баллы (сумма заказов).</param>
/// <param name="LastActionAt">Момент последнего действия (без учёта заказов).</param>
public sealed record UserActivitySegment(
    int EventsAttended,
    int QuestsJoined,
    int QuestsCompleted,
    int QuestsCancelled,
    int SurveysSubmitted,
    int DistinctSurveys,
    int ThanksSent,
    int ThanksReceived,
    int Orders,
    long UnitsOrdered,
    long PointsEarned,
    long PointsSpent,
    DateTime? LastActionAt)
{
    /// <summary>Пустой период: активности нет.</summary>
    public static UserActivitySegment Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);

    /// <summary>Собственные действия сотрудника; полученные благодарности и заказы не входят.</summary>
    public long Actions => EventsAttended + QuestsCompleted + SurveysSubmitted + ThanksSent;

    /// <summary>Признак наличия любой собственной активности.</summary>
    public bool HasActivity => Actions > 0;

    /// <summary>Количество форматов активности с хотя бы одним действием.</summary>
    public int ActiveFormats =>
        (EventsAttended > 0 ? 1 : 0)
        + (QuestsCompleted > 0 ? 1 : 0)
        + (SurveysSubmitted > 0 ? 1 : 0)
        + (ThanksSent > 0 ? 1 : 0);
}

/// <summary>
/// Агрегированные показатели сотрудника за текущий и предыдущий периоды.
/// </summary>
/// <param name="UserId">Идентификатор сотрудника.</param>
/// <param name="DepartmentId">Текущий отдел или <c>null</c>.</param>
/// <param name="Current">Текущий период.</param>
/// <param name="Previous">Предыдущий период той же длины.</param>
public sealed record UserActivityRow(
    Guid UserId,
    int? DepartmentId,
    UserActivitySegment Current,
    UserActivitySegment Previous);
