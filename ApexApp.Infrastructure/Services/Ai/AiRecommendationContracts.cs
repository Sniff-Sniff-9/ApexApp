using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Что именно нужно получить от модели в рекомендациях по вовлечённости.
/// </summary>
public enum AiRecommendationFocus
{
    /// <summary>Идеи активностей и наград.</summary>
    All = 0,

    /// <summary>Только идеи активностей.</summary>
    Activities = 1,

    /// <summary>Только идеи наград.</summary>
    Rewards = 2
}

/// <summary>
/// Запрос рекомендаций по вовлечённости. Окно уже проверено вызывающим кодом,
/// статистика собирается на стороне Backend-а.
/// </summary>
/// <param name="Query">Проверенный запрос аналитики с фильтром отдела.</param>
/// <param name="Focus">Что предложить.</param>
/// <param name="Notes">Необязательные пожелания администратора.</param>
public sealed record AiEngagementRecommendationCommand(
    AnalyticsQuery Query,
    AiRecommendationFocus Focus,
    string? Notes);

/// <summary>
/// Запрос рекомендаций по каталогу наград.
/// </summary>
/// <param name="Query">Проверенный запрос аналитики.</param>
/// <param name="Notes">Необязательные пожелания мерчандайзера.</param>
public sealed record AiProductRecommendationCommand(
    AnalyticsQuery Query,
    string? Notes);

/// <summary>Идея активности для повышения вовлечённости.</summary>
/// <param name="Kind">Формат: <c>quest</c>, <c>mini_game</c>, <c>competition</c>, <c>event</c> или <c>survey</c>.</param>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="WhyItFits">Почему идея подходит данным.</param>
/// <param name="Effort">Трудоёмкость: <c>low</c>, <c>medium</c> или <c>high</c>.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record ActivityIdea(
    string Kind,
    string Title,
    string Description,
    string WhyItFits,
    string Effort,
    string ExpectedImpact);

/// <summary>Идея награды для повышения вовлечённости.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Rationale">Обоснование по данным.</param>
/// <param name="SuggestedPriceBand">Ориентировочный диапазон цены в баллах.</param>
public sealed record RewardIdea(
    string Title,
    string Description,
    string Rationale,
    string? SuggestedPriceBand);

/// <summary>Идея товара для каталога наград.</summary>
/// <param name="Name">Название.</param>
/// <param name="Category">Категория.</param>
/// <param name="Description">Описание.</param>
/// <param name="ShortPitch">Короткий питч для карточки.</param>
/// <param name="PriceBand">Ориентировочный диапазон цены в баллах.</param>
/// <param name="Rationale">Обоснование по данным.</param>
/// <param name="Tags">Теги.</param>
public sealed record ProductIdea(
    string Name,
    string Category,
    string Description,
    string ShortPitch,
    string? PriceBand,
    string Rationale,
    IReadOnlyList<string> Tags);

/// <summary>
/// Рекомендации по вовлечённости: показатели, на которых они основаны, анализ
/// модели, идеи активностей и наград. Ничего не сохраняется в базе данных.
/// </summary>
/// <param name="Engagement">Показатели, переданные модели.</param>
/// <param name="Analysis">Краткий анализ от модели.</param>
/// <param name="Findings">Наблюдения правил, на которые опирались рекомендации.</param>
/// <param name="ActivityIdeas">Идеи активностей.</param>
/// <param name="RewardIdeas">Идеи наград.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record EngagementRecommendationReport(
    AiEngagementSnapshot Engagement,
    string Analysis,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<ActivityIdea> ActivityIdeas,
    IReadOnlyList<RewardIdea> RewardIdeas,
    IReadOnlyList<string> Limitations,
    AiGenerationMeta Meta);

/// <summary>
/// Рекомендации по каталогу наград: показатели спроса, анализ модели, идеи товаров
/// и управленческие действия. Ничего не сохраняется в базе данных.
/// </summary>
/// <param name="Catalog">Показатели, переданные модели.</param>
/// <param name="Analysis">Краткий анализ от модели.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="ProductIdeas">Идеи товаров.</param>
/// <param name="CatalogActions">Действия по каталогу.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record ProductRecommendationReport(
    AiCatalogSnapshot Catalog,
    string Analysis,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<ProductIdea> ProductIdeas,
    IReadOnlyList<string> CatalogActions,
    IReadOnlyList<string> Limitations,
    AiGenerationMeta Meta);

/// <summary>
/// Умные рекомендации для административной панели. Статистика собирается
/// Backend-ом, модель только интерпретирует её и предлагает варианты.
/// </summary>
public interface IAiRecommendationService
{
    /// <summary>Возвращает анализ вовлечённости и идеи активностей и наград.</summary>
    /// <param name="request">Параметры запроса.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<EngagementRecommendationReport>> GetEngagementRecommendationsAsync(
        AiEngagementRecommendationCommand command,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает анализ спроса и идеи товаров для каталога наград.</summary>
    /// <param name="request">Параметры запроса.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<ProductRecommendationReport>> GetProductRecommendationsAsync(
        AiProductRecommendationCommand command,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
