using ApexApp.Infrastructure.Services.Analytics;

namespace ApexApp.Api.DTOs.Analytics;

/// <summary>
/// Спрос на товар каталога.
/// </summary>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="Name">Название.</param>
/// <param name="CategoryId">Идентификатор категории.</param>
/// <param name="Category">Название категории.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="IsDigital">Цифровой товар.</param>
/// <param name="Price">Цена в баллах.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="OrderLines">Строки заказов.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="PointsValue">Стоимость в баллах.</param>
/// <param name="Stock">Текущий остаток.</param>
/// <param name="SellThrough">Доля проданного.</param>
/// <param name="LiveCartUnits">Единицы в корзинах.</param>
/// <param name="IsOutOfStock">Нулевой остаток.</param>
public sealed record RewardCatalogItemDto(
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
    bool IsOutOfStock)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка каталога.</param>
    /// <returns>DTO товара.</returns>
    public static RewardCatalogItemDto From(RewardCatalogRow row) => new(
        row.ProductId,
        row.Name,
        row.CategoryId,
        row.Category,
        row.IsActive,
        row.IsDigital,
        row.Price,
        row.Units,
        row.OrderLines,
        row.UniqueBuyers,
        row.PointsValue,
        row.Stock,
        row.SellThrough,
        row.LiveCartUnits,
        row.IsOutOfStock);
}

/// <summary>
/// Аналитика каталога вознаграждений.
/// </summary>
/// <param name="Items">Товары в порядке убывания спроса.</param>
/// <param name="TotalCount">Всего товаров до ограничения.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardCatalogDto(
    IReadOnlyList<RewardCatalogItemDto> Items,
    int TotalCount,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Результат каталога.</param>
    /// <returns>DTO каталога.</returns>
    public static RewardCatalogDto From(RewardCatalogResult result) => new(
        result.Items.Select(RewardCatalogItemDto.From).ToList(),
        result.TotalCount,
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

/// <summary>
/// Спрос по категории каталога.
/// </summary>
/// <param name="CategoryId">Идентификатор категории.</param>
/// <param name="Name">Название категории.</param>
/// <param name="Products">Количество товаров.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="PointsValue">Стоимость в баллах.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record RewardCategoryDto(
    int? CategoryId,
    string Name,
    long Products,
    long Units,
    long PointsValue,
    long UniqueBuyers,
    bool IsReliable)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка категории.</param>
    /// <returns>DTO категории.</returns>
    public static RewardCategoryDto From(RewardCategoryRow row) => new(
        row.CategoryId,
        row.Name,
        row.Products,
        row.Units,
        row.PointsValue,
        row.UniqueBuyers,
        row.IsReliable);
}

/// <summary>
/// Спрос на вариант товара.
/// </summary>
/// <param name="VariantId">Идентификатор варианта.</param>
/// <param name="ProductId">Идентификатор товара.</param>
/// <param name="ProductName">Название товара.</param>
/// <param name="VariantName">Название варианта.</param>
/// <param name="Category">Категория варианта.</param>
/// <param name="Price">Действующая цена в баллах.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="OrderLines">Строки заказов.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="PointsValue">Стоимость в баллах.</param>
/// <param name="Stock">Текущий остаток.</param>
/// <param name="SellThrough">Доля проданного.</param>
/// <param name="LiveCartUnits">Единицы в корзинах.</param>
/// <param name="IsOutOfStock">Нулевой остаток.</param>
public sealed record RewardVariantItemDto(
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
    bool IsOutOfStock)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка варианта.</param>
    /// <returns>DTO варианта.</returns>
    public static RewardVariantItemDto From(RewardVariantRow row) => new(
        row.VariantId,
        row.ProductId,
        row.ProductName,
        row.VariantName,
        row.Category,
        row.Price,
        row.IsActive,
        row.Units,
        row.OrderLines,
        row.UniqueBuyers,
        row.PointsValue,
        row.Stock,
        row.SellThrough,
        row.LiveCartUnits,
        row.IsOutOfStock);
}

/// <summary>
/// Аналитика вариантов товаров.
/// </summary>
/// <param name="Items">Варианты в порядке убывания спроса.</param>
/// <param name="TotalCount">Всего вариантов до ограничения.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardVariantListDto(
    IReadOnlyList<RewardVariantItemDto> Items,
    int TotalCount,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Результат аналитики вариантов.</param>
    /// <returns>DTO списка вариантов.</returns>
    public static RewardVariantListDto From(RewardVariantResult result) => new(
        result.Items.Select(RewardVariantItemDto.From).ToList(),
        result.TotalCount,
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

/// <summary>
/// Поток баллов по источнику или типу.
/// </summary>
/// <param name="Code">Машинный код.</param>
/// <param name="Title">Название.</param>
/// <param name="Points">Сумма баллов.</param>
/// <param name="Recipients">Количество получателей.</param>
/// <param name="Transactions">Количество транзакций.</param>
public sealed record PointsFlowDto(
    string Code,
    string Title,
    long Points,
    long Recipients,
    long Transactions)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка потока баллов.</param>
    /// <returns>DTO потока.</returns>
    public static PointsFlowDto From(PointsFlowRow row) => new(
        row.Code,
        row.Title,
        row.Points,
        row.Recipients,
        row.Transactions);
}

/// <summary>
/// Экономика баллов.
/// </summary>
/// <param name="EarnedByType">Начисления по типу транзакции.</param>
/// <param name="EarnedBySource">Начисления по источнику активности.</param>
/// <param name="EarnedTotal">Всего начислено.</param>
/// <param name="SpentTotal">Списано за заказы.</param>
/// <param name="OutstandingPersonal">Остаток на личных балансах.</param>
/// <param name="OutstandingReward">Остаток на балансах руководителей.</param>
/// <param name="BurnRate">Отношение списанного к начисленному.</param>
/// <param name="OrdersPointsSpent">Списания по заказам.</param>
/// <param name="LedgerPointsSpent">Списания по журналу.</param>
/// <param name="IsReconciled">Признак совпадения заказов и журнала.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record RewardEconomyDto(
    IReadOnlyList<PointsFlowDto> EarnedByType,
    IReadOnlyList<PointsFlowDto> EarnedBySource,
    long EarnedTotal,
    long SpentTotal,
    long OutstandingPersonal,
    long OutstandingReward,
    double? BurnRate,
    long OrdersPointsSpent,
    long LedgerPointsSpent,
    bool IsReconciled,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="economy">Экономика баллов.</param>
    /// <returns>DTO экономики.</returns>
    public static RewardEconomyDto From(RewardEconomy economy) => new(
        economy.EarnedByType.Select(PointsFlowDto.From).ToList(),
        economy.EarnedBySource.Select(PointsFlowDto.From).ToList(),
        economy.EarnedTotal,
        economy.SpentTotal,
        economy.OutstandingPersonal,
        economy.OutstandingReward,
        economy.BurnRate,
        economy.OrdersPointsSpent,
        economy.LedgerPointsSpent,
        economy.IsReconciled,
        AnalyticsRangeDto.Create(economy.Range),
        economy.Caveats);
}

/// <summary>
/// Заказы в разрезе статуса.
/// </summary>
/// <param name="Status">Машинный код статуса.</param>
/// <param name="Title">Название статуса.</param>
/// <param name="Orders">Количество заказов.</param>
/// <param name="Points">Сумма баллов.</param>
public sealed record OrderStatusDto(
    string Status,
    string Title,
    long Orders,
    long Points)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка статуса.</param>
    /// <returns>DTO статуса.</returns>
    public static OrderStatusDto From(OrderStatusRow row) => new(
        row.Status,
        row.Title,
        row.Orders,
        row.Points);
}

/// <summary>
/// Аналитика заказов.
/// </summary>
/// <param name="Orders">Количество заказов.</param>
/// <param name="Units">Количество единиц.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="Points">Списано баллов.</param>
/// <param name="AverageOrderValue">Средняя стоимость заказа.</param>
/// <param name="AverageProcessingHours">Среднее время обработки, ч.</param>
/// <param name="CancelledOrders">Отменённые заказы.</param>
/// <param name="DeliveredOrders">Выданные заказы.</param>
/// <param name="ByStatus">Распределение по статусам.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record OrderAnalyticsDto(
    long Orders,
    long Units,
    long UniqueBuyers,
    long Points,
    double? AverageOrderValue,
    double? AverageProcessingHours,
    long CancelledOrders,
    long DeliveredOrders,
    IReadOnlyList<OrderStatusDto> ByStatus,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="orders">Аналитика заказов.</param>
    /// <returns>DTO аналитики заказов.</returns>
    public static OrderAnalyticsDto From(OrderAnalytics orders) => new(
        orders.Orders,
        orders.Units,
        orders.UniqueBuyers,
        orders.Points,
        orders.AverageOrderValue,
        orders.AverageProcessingHours,
        orders.CancelledOrders,
        orders.DeliveredOrders,
        orders.ByStatus.Select(OrderStatusDto.From).ToList(),
        AnalyticsRangeDto.Create(orders.Range),
        orders.Caveats);
}

/// <summary>
/// Распределение по уровням вовлечённости.
/// </summary>
/// <param name="Code">Машинный код уровня.</param>
/// <param name="Title">Название уровня.</param>
/// <param name="Rank">Порядковый номер уровня.</param>
/// <param name="Count">Количество сотрудников.</param>
public sealed record EngagementTierDto(
    string Code,
    string Title,
    int Rank,
    long Count)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка распределения.</param>
    /// <returns>DTO уровня.</returns>
    public static EngagementTierDto From(EngagementTierCount row) => new(
        row.Code,
        row.Title,
        row.Rank,
        row.Count);
}

/// <summary>
/// Точка тренда вовлечённости.
/// </summary>
/// <param name="PeriodStart">Начало периода.</param>
/// <param name="PeriodEnd">Конец периода.</param>
/// <param name="WindowStart">Начало окна расчёта.</param>
/// <param name="AverageScore">Средний балл.</param>
/// <param name="MedianScore">Медианный балл.</param>
/// <param name="ScoredEmployees">Сотрудники с активностью.</param>
/// <param name="Headcount">Активные сотрудники.</param>
/// <param name="TopTierCount">Сотрудники в старших уровнях.</param>
/// <param name="PreviousAverageScore">Средний балл предыдущего периода.</param>
/// <param name="ChangePoints">Изменение в пунктах.</param>
/// <param name="Tiers">Распределение по уровням.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record EngagementTrendPointDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime WindowStart,
    double? AverageScore,
    double? MedianScore,
    long ScoredEmployees,
    long Headcount,
    long TopTierCount,
    double? PreviousAverageScore,
    double? ChangePoints,
    IReadOnlyList<EngagementTierDto> Tiers,
    bool IsReliable)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="point">Точка тренда.</param>
    /// <returns>DTO точки.</returns>
    public static EngagementTrendPointDto From(EngagementTrendPoint point) => new(
        point.PeriodStart,
        point.PeriodEnd,
        point.WindowStart,
        point.AverageScore,
        point.MedianScore,
        point.ScoredEmployees,
        point.Headcount,
        point.TopTierCount,
        point.PreviousAverageScore,
        point.ChangePoints,
        point.Tiers.Select(EngagementTierDto.From).ToList(),
        point.IsReliable);
}

/// <summary>
/// Тренд вовлечённости.
/// </summary>
/// <param name="Points">Точки тренда.</param>
/// <param name="WindowDays">Длина окна расчёта.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record EngagementTrendDto(
    IReadOnlyList<EngagementTrendPointDto> Points,
    int WindowDays,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="trend">Тренд вовлечённости.</param>
    /// <returns>DTO тренда.</returns>
    public static EngagementTrendDto From(EngagementTrendResult trend) => new(
        trend.Points.Select(EngagementTrendPointDto.From).ToList(),
        trend.WindowDays,
        AnalyticsRangeDto.Create(trend.Range),
        trend.Caveats);
}

/// <summary>
/// Сотрудник со снижающейся вовлечённостью.
/// </summary>
/// <param name="EmployeeRef">Обезличенный идентификатор.</param>
/// <param name="Name">ФИО или <c>null</c>.</param>
/// <param name="DepartmentId">Идентификатор отдела.</param>
/// <param name="Department">Название отдела.</param>
/// <param name="CurrentScore">Текущий балл.</param>
/// <param name="PreviousScore">Предыдущий балл.</param>
/// <param name="Change">Изменение балла.</param>
/// <param name="Actions">Количество действий.</param>
/// <param name="LastActivityAt">Последняя активность.</param>
public sealed record DecliningEmployeeDto(
    string EmployeeRef,
    string? Name,
    int? DepartmentId,
    string? Department,
    double CurrentScore,
    double PreviousScore,
    double Change,
    long Actions,
    DateTime? LastActivityAt)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="row">Строка сотрудника.</param>
    /// <returns>DTO сотрудника.</returns>
    public static DecliningEmployeeDto From(DecliningEmployee row) => new(
        row.EmployeeRef,
        row.Name,
        row.DepartmentId,
        row.Department,
        row.CurrentScore,
        row.PreviousScore,
        row.Change,
        row.Actions,
        row.LastActivityAt);
}

/// <summary>
/// Список сотрудников со снижающейся вовлечённостью.
/// </summary>
/// <param name="Employees">Сотрудники в порядке падения.</param>
/// <param name="TotalCount">Всего сотрудников в правиле.</param>
/// <param name="SilentEmployees">Активные сотрудники без активности.</param>
/// <param name="EvaluatedEmployees">Сотрудники в расчёте.</param>
/// <param name="Threshold">Порог падения.</param>
/// <param name="Range">Окно расчёта.</param>
/// <param name="Caveats">Ограничения интерпретации.</param>
public sealed record DecliningEmployeesDto(
    IReadOnlyList<DecliningEmployeeDto> Employees,
    int TotalCount,
    long SilentEmployees,
    long EvaluatedEmployees,
    double Threshold,
    AnalyticsRangeDto Range,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="result">Результат списка.</param>
    /// <returns>DTO списка.</returns>
    public static DecliningEmployeesDto From(DecliningEmployeesResult result) => new(
        result.Employees.Select(DecliningEmployeeDto.From).ToList(),
        result.TotalCount,
        result.SilentEmployees,
        result.EvaluatedEmployees,
        result.Threshold,
        AnalyticsRangeDto.Create(result.Range),
        result.Caveats);
}

