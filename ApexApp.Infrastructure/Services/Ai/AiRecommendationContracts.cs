using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Запрос рекомендаций по вовлечённости. Окно уже проверено вызывающим кодом,
/// статистика собирается на стороне Backend-а.
/// </summary>
/// <param name="Query">Проверенный запрос аналитики с фильтром отдела.</param>
/// <param name="Notes">Необязательные пожелания администратора.</param>
public sealed record AiEngagementRecommendationCommand(
    AnalyticsQuery Query,
    string? Notes);

/// <summary>
/// Запрос рекомендаций по каталогу наград.
/// </summary>
/// <param name="Query">Проверенный запрос аналитики.</param>
/// <param name="Notes">Необязательные пожелания мерчандайзера.</param>
public sealed record AiProductRecommendationCommand(
    AnalyticsQuery Query,
    string? Notes);

/// <summary>Рекомендация существующей активности.</summary>
/// <param name="Format">Формат: <c>event</c>, <c>quest</c> или <c>survey</c>.</param>
/// <param name="ActivityId">Идентификатор существующей активности.</param>
/// <param name="Title">Название, взятое из базы.</param>
/// <param name="Action">Рекомендуемое действие.</param>
/// <param name="Rationale">Обоснование по реальным показателям.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record ActivityRecommendation(
    string Format,
    long ActivityId,
    string Title,
    string Action,
    string Rationale,
    string ExpectedImpact);

/// <summary>Рекомендация существующего товара.</summary>
/// <param name="ProductId">Идентификатор существующего товара.</param>
/// <param name="Name">Название, взятое из базы.</param>
/// <param name="Action">Рекомендуемое действие.</param>
/// <param name="Rationale">Обоснование по реальным показателям.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record ProductRecommendation(
    int ProductId,
    string Name,
    string Action,
    string Rationale,
    string ExpectedImpact);

/// <summary>
/// Рекомендации по вовлечённости: показатели, анализ и рекомендации существующих
/// активностей. Ничего не сохраняется в базе данных.
/// </summary>
/// <param name="Engagement">Показатели, переданные модели.</param>
/// <param name="Activities">Существующие активности, переданные модели.</param>
/// <param name="Analysis">Краткий анализ от модели.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="RecommendedActivities">Рекомендации существующих активностей.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record EngagementRecommendationReport(
    AiEngagementSnapshot Engagement,
    IReadOnlyList<AiActivityRow> Activities,
    string Analysis,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<ActivityRecommendation> RecommendedActivities,
    IReadOnlyList<string> Limitations,
    AiGenerationMeta Meta);

/// <summary>
/// Рекомендации по каталогу наград: показатели спроса, анализ и рекомендации
/// существующих товаров. Ничего не сохраняется в базе данных.
/// </summary>
/// <param name="Catalog">Показатели, переданные модели.</param>
/// <param name="Products">Существующие товары, переданные модели.</param>
/// <param name="Analysis">Краткий анализ от модели.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="RecommendedProducts">Рекомендации существующих товаров.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record ProductRecommendationReport(
    AiCatalogSnapshot Catalog,
    IReadOnlyList<AiDemandRow> Products,
    string Analysis,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<ProductRecommendation> RecommendedProducts,
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
