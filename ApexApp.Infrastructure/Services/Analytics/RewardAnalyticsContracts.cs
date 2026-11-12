namespace ApexApp.Infrastructure.Services.Analytics;

using ApexApp.Infrastructure.Common;

/// <summary>
/// Спрос на товар каталога. Показатели не смешиваются: количество заказов,
/// количество единиц, баллы и уникальные покупатели — разные величины.
/// </summary>
/// <param name="ProductId">Идентификатор товара; <c>null</c> для удалённых товаров.</param>
/// <param name="Name">Название товара.</param>
/// <param name="CategoryId">Идентификатор категории.</param>
/// <param name="Category">Категория.</param>
/// <param name="IsActive">Признак активности товара.</param>
/// <param name="IsDigital">Цифровой товар (не может быть заказан).</param>
/// <param name="Price">Текущая цена в баллах.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="OrderLines">Количество строк заказов.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="PointsValue">Стоимость заказанного в баллах.</param>
/// <param name="Stock">Текущий остаток; <c>null</c> — без ограничения.</param>
/// <param name="SellThrough">Доля проданного от (проданное + остаток).</param>
/// <param name="LiveCartUnits">Единицы в текущих корзинах.</param>
/// <param name="IsOutOfStock">Признак нулевого остатка.</param>
public sealed record RewardCatalogRow(
    int? ProductId,
    string Name,
    int? CategoryId,
    string? Category,
    bool IsActive,
    bool IsDigital,
    long Price,
    long Units,
    long OrderLines,
    long UniqueBuyers,
    long PointsValue,
    int? Stock,
    double? SellThrough,
    long LiveCartUnits,
    bool IsOutOfStock);

/// <summary>
/// Спрос по категории каталога.
/// </summary>
/// <param name="CategoryId">Идентификатор категории; <c>null</c> для товаров без категории.</param>
/// <param name="Name">Название категории.</param>
/// <param name="Products">Количество товаров категории.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="PointsValue">Стоимость заказанного в баллах.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record RewardCategoryRow(
    int? CategoryId,
    string Name,
    long Products,
    long Units,
    long PointsValue,
    long UniqueBuyers,
    bool IsReliable);

/// <summary>
/// Результат аналитики каталога.
/// </summary>
/// <param name="Items">Товары в порядке убывания спроса.</param>
/// <param name="TotalCount">Общее количество товаров до ограничения «топ N».</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardCatalogResult(
    IReadOnlyList<RewardCatalogRow> Items,
    int TotalCount,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Поток баллов по типу транзакции или источнику награды.
/// </summary>
/// <param name="Code">Машинный код.</param>
/// <param name="Title">Название для отображения.</param>
/// <param name="Points">Сумма баллов.</param>
/// <param name="Recipients">Количество получателей.</param>
/// <param name="Transactions">Количество транзакций.</param>
public sealed record PointsFlowRow(
    string Code,
    string Title,
    long Points,
    long Recipients,
    long Transactions);

/// <summary>
/// Экономика баллов: начисления, списания, остатки и сверка с заказами.
/// </summary>
/// <param name="EarnedByType">Начисления по типу транзакции.</param>
/// <param name="EarnedBySource">Начисления по источнику активности.</param>
/// <param name="EarnedTotal">Всего начислено за окно.</param>
/// <param name="SpentTotal">Списано за заказы окна.</param>
/// <param name="OutstandingPersonal">Остаток на личных балансах.</param>
/// <param name="OutstandingReward">Остаток на балансах руководителей.</param>
/// <param name="BurnRate">Отношение списанного к начисленному.</param>
/// <param name="OrdersPointsSpent">Списания по заказам окна.</param>
/// <param name="LedgerPointsSpent">Списания по журналу транзакций окна.</param>
/// <param name="IsReconciled">Признак совпадения заказов и журнала.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardEconomy(
    IReadOnlyList<PointsFlowRow> EarnedByType,
    IReadOnlyList<PointsFlowRow> EarnedBySource,
    long EarnedTotal,
    long SpentTotal,
    long OutstandingPersonal,
    long OutstandingReward,
    double? BurnRate,
    long OrdersPointsSpent,
    long LedgerPointsSpent,
    bool IsReconciled,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Заказы в разрезе статуса.
/// </summary>
/// <param name="Status">Машинный код статуса.</param>
/// <param name="Title">Название статуса.</param>
/// <param name="Orders">Количество заказов.</param>
/// <param name="Points">Сумма баллов.</param>
public sealed record OrderStatusRow(
    string Status,
    string Title,
    long Orders,
    long Points);

/// <summary>
/// Аналитика заказов.
/// </summary>
/// <param name="Orders">Количество заказов (без отменённых и возвращённых).</param>
/// <param name="Units">Количество единиц товара.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="Points">Списано баллов.</param>
/// <param name="AverageOrderValue">Средняя стоимость заказа в баллах.</param>
/// <param name="AverageProcessingHours">Среднее время обработки доставленного заказа, ч.</param>
/// <param name="CancelledOrders">Отменённые заказы.</param>
/// <param name="DeliveredOrders">Доставленные заказы.</param>
/// <param name="ByStatus">Распределение по статусам.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record OrderAnalytics(
    long Orders,
    long Units,
    long UniqueBuyers,
    long Points,
    double? AverageOrderValue,
    double? AverageProcessingHours,
    long CancelledOrders,
    long DeliveredOrders,
    IReadOnlyList<OrderStatusRow> ByStatus,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Спрос на вариант товара. Вариант хранит собственные цену и остаток, поэтому
/// показатели считаются отдельно от товара.
/// </summary>
/// <param name="VariantId">Идентификатор варианта.</param>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Название товара.</param>
/// <param name="VariantName">Название варианта.</param>
/// <param name="Category">Категория варианта.</param>
/// <param name="Price">Действующая цена варианта в баллах.</param>
/// <param name="IsActive">Признак активности варианта.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="OrderLines">Количество строк заказов.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="PointsValue">Стоимость заказанного в баллах.</param>
/// <param name="Stock">Текущий остаток варианта.</param>
/// <param name="SellThrough">Доля проданного от (проданное + остаток).</param>
/// <param name="LiveCartUnits">Единицы в текущих корзинах.</param>
/// <param name="IsOutOfStock">Признак нулевого остатка.</param>
public sealed record RewardVariantRow(
    int VariantId,
    int ProductId,
    string ProductName,
    string VariantName,
    string? Category,
    long Price,
    bool IsActive,
    long Units,
    long OrderLines,
    long UniqueBuyers,
    long PointsValue,
    int Stock,
    double? SellThrough,
    long LiveCartUnits,
    bool IsOutOfStock);

/// <summary>
/// Результат аналитики вариантов товаров.
/// </summary>
/// <param name="Items">Варианты в порядке убывания спроса.</param>
/// <param name="TotalCount">Общее количество вариантов до ограничения «топ N».</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardVariantResult(
    IReadOnlyList<RewardVariantRow> Items,
    int TotalCount,
    AnalyticsRange Range,
    IReadOnlyList<string> Caveats);

/// <summary>
/// Аналитика вознаграждений и каталога.
/// </summary>
public interface IRewardAnalyticsService
{
    /// <summary>Спрос на товары каталога.</summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<RewardCatalogResult>> GetCatalogAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Спрос на варианты товаров.</summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<RewardVariantResult>> GetVariantsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Спрос по категориям каталога.</summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<IReadOnlyList<RewardCategoryRow>>> GetCategoriesAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Экономика баллов и сверка с заказами.</summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<RewardEconomy>> GetEconomyAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Аналитика заказов.</summary>
    /// <param name="query">Запрос клиента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат или ошибка 400.</returns>
    Task<ServiceResult<OrderAnalytics>> GetOrdersAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
