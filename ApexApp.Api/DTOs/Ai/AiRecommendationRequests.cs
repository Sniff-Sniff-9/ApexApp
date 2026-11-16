using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>
/// Параметры рекомендаций по вовлечённости. Окно задаётся так же, как в аналитике.
/// </summary>
/// <param name="From">Начало окна (UTC); по умолчанию — 30 дней назад.</param>
/// <param name="To">Конец окна (UTC); по умолчанию — текущий момент.</param>
/// <param name="Granularity">Гранулярность рядов: <c>day</c>, <c>week</c> или <c>month</c>.</param>
/// <param name="DepartmentId">Ограничение по отделу; необязательно.</param>
/// <param name="Top">Размер списков «топ N».</param>
/// <param name="Focus">Что предложить: <c>all</c>, <c>activities</c> или <c>rewards</c>.</param>
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

/// <summary>Машинные коды и разбор фокуса рекомендаций.</summary>
public static class AiRecommendationFocusCodes
{
    /// <summary>Идеи активностей и наград.</summary>
    public const string All = "all";

    /// <summary>Только идеи активностей.</summary>
    public const string Activities = "activities";

    /// <summary>Только идеи наград.</summary>
    public const string Rewards = "rewards";

    /// <summary>
    /// Разбирает фокус рекомендаций. Пустое значение трактуется как <c>all</c>.
    /// </summary>
    /// <param name="value">Строка запроса.</param>
    /// <param name="focus">Разобранный фокус.</param>
    /// <returns><c>true</c>, если значение корректно.</returns>
    public static bool TryParse(string? value, out AiRecommendationFocus focus)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or All:
                focus = AiRecommendationFocus.All;
                return true;
            case Activities:
                focus = AiRecommendationFocus.Activities;
                return true;
            case Rewards:
                focus = AiRecommendationFocus.Rewards;
                return true;
            default:
                focus = AiRecommendationFocus.All;
                return false;
        }
    }
}
