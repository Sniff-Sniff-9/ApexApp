namespace ApexApp.Api.DTOs.Ai;

/// <summary>
/// Параметры рекомендаций по вовлечённости. Окно задаётся так же, как в аналитике.
/// </summary>
/// <param name="From">Начало окна (UTC); по умолчанию — 30 дней назад.</param>
/// <param name="To">Конец окна (UTC); по умолчанию — текущий момент.</param>
/// <param name="Granularity">Гранулярность рядов: <c>day</c>, <c>week</c> или <c>month</c>.</param>
/// <param name="DepartmentId">Ограничение по отделу; необязательно.</param>
/// <param name="Top">Размер списков «топ N».</param>
/// <param name="Focus">Совместимость: <c>all</c> или <c>activities</c>; <c>rewards</c> больше не поддерживается.</param>
/// <param name="Notes">Пожелания администратора; необязательно.</param>
public sealed record AiEngagementRecommendationRequest(
    DateTime? From = null,
    DateTime? To = null,
    string? Granularity = null,
    int? DepartmentId = null,
    int Top = 10,
    string? Focus = null,
    string? Notes = null);

/// <summary>Параметры рекомендаций по каталогу наград.</summary>
/// <param name="From">Начало окна (UTC); по умолчанию — 30 дней назад.</param>
/// <param name="To">Конец окна (UTC); по умолчанию — текущий момент.</param>
/// <param name="Granularity">Гранулярность рядов: <c>day</c>, <c>week</c> или <c>month</c>.</param>
/// <param name="Top">Размер списков «топ N».</param>
/// <param name="Notes">Пожелания мерчандайзера; необязательно.</param>
public sealed record AiProductRecommendationRequest(
    DateTime? From = null,
    DateTime? To = null,
    string? Granularity = null,
    int Top = 10,
    string? Notes = null);

/// <summary>Допустимые значения фокуса рекомендаций.</summary>
public static class AiRecommendationFocusCodes
{
    /// <summary>Все существующие активности.</summary>
    public const string All = "all";

    /// <summary>Только существующие активности.</summary>
    public const string Activities = "activities";

    /// <summary>
    /// Проверяет фокус. Пустое значение трактуется как <c>all</c>.
    /// </summary>
    /// <param name="value">Строка запроса.</param>
    /// <returns><c>true</c>, если значение корректно.</returns>
    public static bool IsSupported(string? value) =>
        value?.Trim().ToLowerInvariant() is null or "" or All or Activities;
}
