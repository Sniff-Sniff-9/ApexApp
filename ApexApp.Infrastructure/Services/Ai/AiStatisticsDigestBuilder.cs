using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Собирает сводку показателей для модели из уже рассчитанной аналитики. Сервис не
/// выполняет собственных SQL-запросов и не обращается к <c>AppDbContext</c>: он
/// переиспользует обзор платформы, наблюдения правил, тренд вовлечённости и
/// аналитику вознаграждений. В сводку не попадают персональные данные сотрудников.
/// </summary>
public sealed class AiStatisticsDigestBuilder(
    IAnalyticsOverviewService overviewService,
    IAnalyticsInsightService insightService,
    IEngagementAnalyticsService engagementService,
    IRewardAnalyticsService rewardService,
    IActivityAnalyticsService activityService,
    IOptions<DeepSeekOptions> options,
    ILogger<AiStatisticsDigestBuilder> logger)
{
    private const int TopDemandRows = 12;
    private const int CatalogSampleSize = 50;
    private const int ActivitySampleSize = 50;
    private const int ActivityRows = 20;
    private const int StockRiskRows = 8;
    private const int MaxFindings = 12;
    private const int MaxLimitations = 10;
    private const int MaxLimitationLength = 400;

    private readonly DeepSeekOptions _options = options.Value;

    /// <summary>
    /// Формирует сводку вовлечённости за окно.
    /// </summary>
    /// <param name="query">Запрос с проверенным фильтром отдела.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка или ошибка расчёта показателей.</returns>
    public async Task<ServiceResult<AiEngagementDigest>> BuildEngagementAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var overviewResult = await overviewService.GetAsync(query, cancellationToken);

        if (!overviewResult.IsSuccess)
        {
            return ServiceResult<AiEngagementDigest>.Fail(
                overviewResult.StatusCode,
                overviewResult.Error!);
        }

        var overview = overviewResult.Value!;
        var range = overview.Range;

        var insightResult = await insightService.GetAsync(query, cancellationToken);

        IReadOnlyList<AiFinding> findings = insightResult.IsSuccess
            ? ToFindings(insightResult.Value!.Insights)
            : Array.Empty<AiFinding>();

        var trendResult = await engagementService.GetTrendAsync(query, cancellationToken);
        var trendPoint = trendResult.IsSuccess
            ? trendResult.Value!.Points.LastOrDefault()
            : null;

        var decliningResult = await engagementService.GetDecliningAsync(
            query,
            includeNames: false,
            cancellationToken);

        var declining = decliningResult.IsSuccess ? decliningResult.Value! : null;

        var activityResult = await activityService.GetActivitiesAsync(
            query with { Top = ActivitySampleSize },
            format: null,
            ActivitySortBy.Participants,
            cancellationToken);

        IReadOnlyList<AiActivityRow> activities = activityResult.IsSuccess
            ? activityResult.Value!.Items
                .Take(ActivityRows)
                .Select(row => new AiActivityRow(
                    row.Format,
                    row.Id,
                    row.Title,
                    row.IsActive,
                    row.Participants,
                    row.Completed,
                    row.CompletionRate,
                    row.RewardAmount,
                    row.StartsAt,
                    row.EndsAt))
                .ToList()
            : Array.Empty<AiActivityRow>();

        var snapshot = new AiEngagementSnapshot(
            Describe(range),
            Metric(overview.Headcount, "active_users"),
            Metric(overview.Participation, "participants"),
            Rate(overview.Participation, "participation_rate"),
            trendPoint?.AverageScore,
            trendPoint?.ChangePoints,
            declining?.SilentEmployees,
            declining?.TotalCount,
            overview.Formats.Select(ToFormatSnapshot).ToList());

        var limitations = new List<string>();

        foreach (var format in overview.Formats.Where(format => !format.IsReliable))
        {
            limitations.Add(
                $"Формат «{format.Title}»: выборка меньше минимальной, доли не показываются.");
        }

        if (!insightResult.IsSuccess)
        {
            limitations.Add("Наблюдения правил аналитики недоступны в этом запросе.");
        }

        if (!trendResult.IsSuccess)
        {
            limitations.Add("Тренд вовлечённости недоступен в этом запросе.");
        }

        if (!decliningResult.IsSuccess)
        {
            limitations.Add("Данные о молчащих сотрудниках недоступны в этом запросе.");
        }

        if (!activityResult.IsSuccess)
        {
            limitations.Add("Список существующих активностей недоступен в этом запросе.");
        }

        if (query.DepartmentId is not null)
        {
            limitations.Add(
                "Список активностей общий для платформы: фильтр отдела влияет только на агрегированные показатели.");
        }

        var normalized = AiContentValidator.StringList(limitations, MaxLimitations, MaxLimitationLength);
        var digest = new AiEngagementDigest(snapshot, findings, normalized, activities, string.Empty);
        var text = Truncate(AiDigestWriter.Engagement(digest));

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "AI engagement digest built: period={Period}, findings={Findings}, digestChars={Chars}.",
                snapshot.Period,
                findings.Count,
                text.Length);
        }

        return ServiceResult<AiEngagementDigest>.Ok(digest with { Text = text });
    }

    /// <summary>
    /// Формирует сводку каталога наград и спроса за окно.
    /// </summary>
    /// <param name="query">Запрос аналитики.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка или ошибка расчёта показателей.</returns>
    public async Task<ServiceResult<AiProductDigest>> BuildProductsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestedTop = query.Top <= 0 ? CatalogSampleSize : query.Top;
        var catalogQuery = query with
        {
            Top = Math.Clamp(requestedTop, TopDemandRows, CatalogSampleSize)
        };

        var catalogResult = await rewardService.GetCatalogAsync(catalogQuery, cancellationToken);

        if (!catalogResult.IsSuccess)
        {
            return ServiceResult<AiProductDigest>.Fail(
                catalogResult.StatusCode,
                catalogResult.Error!);
        }

        var catalog = catalogResult.Value!;
        var categoriesResult = await rewardService.GetCategoriesAsync(query, cancellationToken);
        var economyResult = await rewardService.GetEconomyAsync(query, cancellationToken);
        var ordersResult = await rewardService.GetOrdersAsync(query, cancellationToken);
        var insightResult = await insightService.GetAsync(query, cancellationToken);

        var economy = economyResult.IsSuccess ? economyResult.Value! : null;
        var orders = ordersResult.IsSuccess ? ordersResult.Value! : null;

        var categories = categoriesResult.IsSuccess
            ? categoriesResult.Value!
                .Select(row => new AiCategorySnapshot(
                    row.Name,
                    row.Products,
                    row.Units,
                    row.UniqueBuyers,
                    row.IsReliable))
                .ToList()
            : new List<AiCategorySnapshot>();

        var snapshot = new AiCatalogSnapshot(
            catalog.TotalCount,
            orders?.Orders ?? 0,
            orders?.Units ?? 0,
            orders?.UniqueBuyers ?? 0,
            orders?.AverageOrderValue,
            economy?.EarnedTotal ?? 0,
            economy?.SpentTotal ?? 0,
            economy?.OutstandingPersonal ?? 0,
            economy?.BurnRate,
            categories);

        var topDemand = catalog.Items
            .Where(row => row.Units > 0)
            .OrderByDescending(row => row.Units)
            .Take(TopDemandRows)
            .Select(ToDemandRow)
            .ToList();

        var topDemandIds = topDemand
            .Where(row => row.ProductId is not null)
            .Select(row => row.ProductId!.Value)
            .ToHashSet();

        var stockRisks = catalog.Items
            .Where(row => row.ProductId is not null
                          && row.IsActive
                          && !topDemandIds.Contains(row.ProductId.Value)
                          && (row.IsOutOfStock || row.Units == 0))
            .OrderBy(row => row.Stock ?? int.MaxValue)
            .ThenBy(row => row.Name)
            .Take(StockRiskRows)
            .Select(ToDemandRow)
            .ToList();

        IReadOnlyList<AiFinding> findings = insightResult.IsSuccess
            ? ToFindings(insightResult.Value!.Insights)
            : Array.Empty<AiFinding>();

        var limitations = new List<string>();

        if (!categoriesResult.IsSuccess)
        {
            limitations.Add("Спрос по категориям недоступен в этом запросе.");
        }

        if (!insightResult.IsSuccess)
        {
            limitations.Add("Наблюдения правил аналитики недоступны в этом запросе.");
        }

        if (!ordersResult.IsSuccess)
        {
            limitations.Add("Аналитика заказов недоступна в этом запросе.");
        }

        if (!economyResult.IsSuccess)
        {
            limitations.Add("Экономика баллов недоступна в этом запросе.");
        }

        if (catalog.Items.Count < catalog.TotalCount)
        {
            limitations.Add(
                $"В сводку спроса попали {catalog.Items.Count} из {catalog.TotalCount} товаров.");
        }

        var normalized = AiContentValidator.StringList(limitations, MaxLimitations, MaxLimitationLength);
        var digest = new AiProductDigest(
            snapshot,
            topDemand,
            stockRisks,
            findings,
            normalized,
            string.Empty);
        var text = Truncate(AiDigestWriter.Products(digest));

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "AI catalog digest built: products={Products}, demandRows={DemandRows}, digestChars={Chars}.",
                snapshot.TotalProducts,
                topDemand.Count,
                text.Length);
        }

        return ServiceResult<AiProductDigest>.Ok(digest with { Text = text });
    }

    private static long Metric(IReadOnlyList<MetricValue> metrics, string code) =>
        metrics.FirstOrDefault(metric => metric.Code == code)?.Numerator ?? 0;

    private static double? Rate(IReadOnlyList<MetricValue> metrics, string code) =>
        metrics.FirstOrDefault(metric => metric.Code == code)?.Value;

    private static string Describe(AnalyticsRange range) =>
        $"{range.From:yyyy-MM-dd} — {range.To.AddDays(-1):yyyy-MM-dd} " +
        $"({range.LengthDays:0} дн., {range.TimeZone})";

    private static AiFormatSnapshot ToFormatSnapshot(ActivityFormatRow row) =>
        new(
            row.Format,
            row.Title,
            row.Activities,
            row.Participants,
            row.ReachRate,
            row.CompletionRate,
            row.RewardClaimRate,
            row.IsReliable);

    private static AiDemandRow ToDemandRow(RewardCatalogRow row) =>
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

    private static IReadOnlyList<AiFinding> ToFindings(IReadOnlyList<Insight> insights) =>
        insights
            .Take(MaxFindings)
            .Select(insight => new AiFinding(
                insight.Code,
                insight.Severity,
                insight.Title,
                insight.Description))
            .ToList();

    private string Truncate(string text)
    {
        var limit = Math.Clamp(_options.MaxPromptChars, 2000, 40000);

        if (text.Length <= limit)
        {
            return text;
        }

        var cut = text.LastIndexOf('\n', limit - 1);

        return cut > 0 ? text[..cut] : text[..limit];
    }
}
