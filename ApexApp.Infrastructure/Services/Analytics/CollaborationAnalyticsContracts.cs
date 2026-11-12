namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Пара отделов в матрице взаимодействия.
/// </summary>
/// <param name="FromDepartmentId">Отдел отправителя.</param>
/// <param name="FromDepartment">Название отдела отправителя.</param>
/// <param name="ToDepartmentId">Отдел получателя.</param>
/// <param name="ToDepartment">Название отдела получателя.</param>
/// <param name="Count">Количество благодарностей.</param>
/// <param name="IsCrossDepartment">Признак межотдельского взаимодействия.</param>
public sealed record DepartmentInteractionRow(
    int? FromDepartmentId,
    string FromDepartment,
    int? ToDepartmentId,
    string ToDepartment,
    long Count,
    bool IsCrossDepartment);

/// <summary>
/// Точка ряда межотдельских благодарностей.
/// </summary>
/// <param name="PeriodStart">Начало корзины (UTC).</param>
/// <param name="PeriodEnd">Конец корзины (UTC).</param>
/// <param name="Total">Всего благодарностей.</param>
/// <param name="CrossDepartment">Благодарности сотрудникам других отделов.</param>
/// <param name="CrossDepartmentShare">Доля межотдельских благодарностей.</param>
public sealed record CrossDepartmentSeriesPoint(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    long Total,
    long CrossDepartment,
    double? CrossDepartmentShare);

/// <summary>
/// Взаимодействие отделов через благодарности.
/// </summary>
/// <param name="Total">Всего благодарностей за окно.</param>
/// <param name="CrossDepartment">Межотдельские благодарности за окно.</param>
/// <param name="CrossDepartmentShare">Доля межотдельских благодарностей.</param>
/// <param name="PreviousCrossDepartmentShare">Доля в предыдущем окне.</param>
/// <param name="TopPairs">Наиболее частые пары отделов.</param>
/// <param name="Series">Ряд доли межотдельских благодарностей.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record CollaborationThanksResult(
    long Total,
    long CrossDepartment,
    double? CrossDepartmentShare,
    double? PreviousCrossDepartmentShare,
    IReadOnlyList<DepartmentInteractionRow> TopPairs,
    IReadOnlyList<CrossDepartmentSeriesPoint> Series,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Аналитика межотдельского взаимодействия.
/// </summary>
public interface ICollaborationAnalyticsService
{
    /// <summary>
    /// Возвращает матрицу благодарностей между отделами. Доступно только безопасное
    /// агрегированное представление: пары отделов без имён сотрудников.
    /// </summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<CollaborationThanksResult>> GetThanksAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
