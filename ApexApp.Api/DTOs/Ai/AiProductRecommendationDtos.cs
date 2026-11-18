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

/// <summary>Существующий товар, переданный модели.</summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="Name">Название.</param>
/// <param name="CategoryId">Категория.</param>
/// <param name="Category">Название категории.</param>
/// <param name="Price">Цена.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="UniqueBuyers">Покупатели.</param>
/// <param name="LiveCartUnits">Единицы в корзинах.</param>
/// <param name="Stock">Остаток.</param>
/// <param name="SellThrough">Доля проданного.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="IsOutOfStock">Нет в наличии.</param>
/// <param name="IsDigital">Цифровой товар.</param>
public sealed record AiProductSummaryDto(
    int? ProductId,
    string Name,
    int? CategoryId,
    string? Category,
    long Price,
    long Units,
    long UniqueBuyers,
    long LiveCartUnits,
    int? Stock,
    double? SellThrough,
    bool IsActive,
    bool IsOutOfStock,
    bool IsDigital)
{
    /// <summary>Создаёт DTO из строки товара.</summary>
    /// <param name="row">Строка товара.</param>
    /// <returns>DTO товара.</returns>
    public static AiProductSummaryDto From(AiDemandRow row) =>
        new(
            row.ProductId,
            row.Name,
            row.CategoryId,
            row.Category,
            row.Price,
            row.Units,
            row.UniqueBuyers,
            row.LiveCartUnits,
            row.Stock,
            row.SellThrough,
            row.IsActive,
            row.IsOutOfStock,
            row.IsDigital);
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

/// <summary>Рекомендация существующего товара.</summary>
/// <param name="ProductId">Идентификатор существующего товара.</param>
/// <param name="Name">Название из базы.</param>
/// <param name="Action">Рекомендуемое действие.</param>
/// <param name="Rationale">Обоснование по реальным показателям.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record CatalogRecommendationDto(
    int ProductId,
    string Name,
    string Action,
    string Rationale,
    string ExpectedImpact)
{
    /// <summary>Создаёт DTO из рекомендации.</summary>
    /// <param name="recommendation">Рекомендация.</param>
    /// <returns>DTO рекомендации.</returns>
    public static CatalogRecommendationDto From(ProductRecommendation recommendation) =>
        new(
            recommendation.ProductId,
            recommendation.Name,
            recommendation.Action,
            recommendation.Rationale,
            recommendation.ExpectedImpact);
}

/// <summary>Отчёт рекомендаций по каталогу наград.</summary>
/// <param name="Catalog">Показатели, переданные модели.</param>
/// <param name="Products">Существующие товары.</param>
/// <param name="Analysis">Краткий анализ.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="RecommendedProducts">Рекомендации существующих товаров.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
public sealed record ProductRecommendationDto(
    CatalogSummaryDto Catalog,
    IReadOnlyList<AiProductSummaryDto> Products,
    string Analysis,
    IReadOnlyList<AiFindingDto> Findings,
    IReadOnlyList<CatalogRecommendationDto> RecommendedProducts,
    IReadOnlyList<string> Limitations)
{
    /// <summary>Создаёт DTO из отчёта сервиса.</summary>
    /// <param name="report">Отчёт.</param>
    /// <returns>DTO отчёта.</returns>
    public static ProductRecommendationDto From(ProductRecommendationReport report) =>
        new(
            CatalogSummaryDto.From(report.Catalog),
            report.Products.Select(AiProductSummaryDto.From).ToList(),
            report.Analysis,
            report.Findings.Select(AiFindingDto.From).ToList(),
            report.RecommendedProducts.Select(CatalogRecommendationDto.From).ToList(),
            report.Limitations);
}
