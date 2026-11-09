namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Вклад одного источника активности в итоговый балл вовлечённости.
/// </summary>
/// <param name="Code">Машинный код источника.</param>
/// <param name="Title">Название источника для отображения.</param>
/// <param name="Count">Количество учтённых действий (уникальных сущностей).</param>
/// <param name="Points">Начисленные баллы за источник.</param>
/// <param name="MaxPoints">Максимум баллов по источнику.</param>
public sealed record EngagementBreakdown(
    string Code,
    string Title,
    int Count,
    double Points,
    double MaxPoints);

/// <summary>
/// Рассчитанный статус вовлечённости пользователя за скользящее окно.
/// </summary>
/// <param name="Status">Машинный код уровня.</param>
/// <param name="StatusTitle">Название уровня для отображения.</param>
/// <param name="StatusRank">Порядковый номер уровня (1–5).</param>
/// <param name="Score">Итоговый балл (0–100).</param>
/// <param name="MaxScore">Максимально возможный балл.</param>
/// <param name="NextStatus">Код следующего уровня; <c>null</c> для максимального уровня.</param>
/// <param name="NextStatusTitle">Название следующего уровня; <c>null</c> для максимального уровня.</param>
/// <param name="NextStatusScore">Балл, необходимый для следующего уровня; <c>null</c> для максимального уровня.</param>
/// <param name="PointsToNext">Сколько баллов не хватает до следующего уровня; <c>null</c> для максимального уровня.</param>
/// <param name="NextLevelProgress">Прогресс внутри текущего уровня к следующему: от 0 до 1.</param>
/// <param name="WindowDays">Длина расчётного окна в днях.</param>
/// <param name="WindowStart">Начало расчётного окна (UTC).</param>
/// <param name="WindowEnd">Конец расчётного окна, момент расчёта (UTC).</param>
/// <param name="LastActivityAt">Последнее учтённое действие внутри окна; <c>null</c>, если активности нет.</param>
/// <param name="Breakdown">Разбивка баллов по источникам активности.</param>
/// <param name="ComputedAt">Момент расчёта (UTC).</param>
public sealed record EngagementSummary(
    string Status,
    string StatusTitle,
    int StatusRank,
    double Score,
    double MaxScore,
    string? NextStatus,
    string? NextStatusTitle,
    double? NextStatusScore,
    double? PointsToNext,
    double NextLevelProgress,
    int WindowDays,
    DateTime WindowStart,
    DateTime WindowEnd,
    DateTime? LastActivityAt,
    IReadOnlyList<EngagementBreakdown> Breakdown,
    DateTime ComputedAt);
