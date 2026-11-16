using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>Спрос по категории каталога.</summary>
/// <param name="Name">Название категории.</param>
/// <param name="Products">Количество товаров.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record CategorySummaryDto(
    string Name,
    long Products,
    long Units,
    long UniqueBuyers,
    bool IsReliable)
{
    /// <summary>Создаёт DTO из сводки категории.</summary>
    /// <param name="category">Сводка категории.</param>
    /// <returns>DTO сводки.</returns>
    public static CategorySummaryDto From(AiCategorySnapshot category) =>
        new(category.Name, category.Products, category.Units, category.UniqueBuyers, category.IsReliable);
}

/// <summary>Показатели каталога, переданные модели.</summary>
/// <param name="TotalProducts">Всего товаров в каталоге.</param>
/// <param name="Orders">Заказы за окно.</param>
/// <param name="Units">Единицы товара за окно.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="AverageOrderValue">Средняя стоимость заказа в баллах.</param>
/// <param name="PointsEarned">Начислено баллов.</param>
/// <param name="PointsSpent">Списано баллов.</param>
/// <param name="OutstandingPoints">Остаток на личных балансах.</param>
/// <param name="BurnRate">Доля списания от начисленного.</param>
/// <param name="Categories">Спрос по категориям.</param>
public sealed record CatalogSummaryDto(
    long TotalProducts,
    long Orders,
    long Units,
    long UniqueBuyers,
    double? AverageOrderValue,
    long PointsEarned,
    long PointsSpent,
    long OutstandingPoints,
    double? BurnRate,
    IReadOnlyList<CategorySummaryDto> Categories)
{
    /// <summary>Создаёт DTO из сводки каталога.</summary>
    /// <param name="snapshot">Сводка каталога.</param>
    /// <returns>DTO сводки.</returns>
    public static CatalogSummaryDto From(AiCatalogSnapshot snapshot) =>
        new(
            snapshot.TotalProducts,
            snapshot.Orders,
            snapshot.Units,
            snapshot.UniqueBuyers,
            snapshot.AverageOrderValue,
            snapshot.PointsEarned,
            snapshot.PointsSpent,
            snapshot.OutstandingPoints,
            snapshot.BurnRate,
            snapshot.Categories.Select(CategorySummaryDto.From).ToList());
}

/// <summary>Идея товара для каталога наград.</summary>
/// <param name="Name">Название.</param>
/// <param name="Category">Категория.</param>
/// <param name="Description">Описание.</param>
/// <param name="ShortPitch">Короткий питч для карточки.</param>
/// <param name="PriceBand">Ориентировочный диапазон цены в баллах.</param>
/// <param name="Rationale">Обоснование по данным.</param>
/// <param name="Tags">Теги.</param>
public sealed record ProductIdeaDto(
    string Name,
    string Category,
    string Description,
    string ShortPitch,
    string? PriceBand,
    string Rationale,
    IReadOnlyList<string> Tags)
{
    /// <summary>Создаёт DTO из идеи товара.</summary>
    /// <param name="idea">Идея товара.</param>
    /// <returns>DTO идеи.</returns>
    public static ProductIdeaDto From(ProductIdea idea) =>
        new(
            idea.Name,
            idea.Category,
            idea.Description,
            idea.ShortPitch,
            idea.PriceBand,
            idea.Rationale,
            idea.Tags);
}

/// <summary>Отчёт рекомендаций по каталогу наград.</summary>
/// <param name="Catalog">Показатели, переданные модели.</param>
/// <param name="Analysis">Краткий анализ.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="ProductIdeas">Идеи товаров.</param>
/// <param name="CatalogActions">Действия по каталогу.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
public sealed record ProductRecommendationDto(
    CatalogSummaryDto Catalog,
    string Analysis,
    IReadOnlyList<AiFindingDto> Findings,
    IReadOnlyList<ProductIdeaDto> ProductIdeas,
    IReadOnlyList<string> CatalogActions,
    IReadOnlyList<string> Limitations)
{
    /// <summary>Создаёт DTO из отчёта сервиса.</summary>
    /// <param name="report">Отчёт.</param>
    /// <returns>DTO отчёта.</returns>
    public static ProductRecommendationDto From(ProductRecommendationReport report) =>
        new(
            CatalogSummaryDto.From(report.Catalog),
            report.Analysis,
            report.Findings.Select(AiFindingDto.From).ToList(),
            report.ProductIdeas.Select(ProductIdeaDto.From).ToList(),
            report.CatalogActions,
            report.Limitations);
}
