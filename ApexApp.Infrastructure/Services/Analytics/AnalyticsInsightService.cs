using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class AnalyticsInsightService(
    AppDbContext dbContext,
    IAnalyticsOverviewService overviewService,
    IDepartmentAnalyticsService departmentService,
    ICollaborationAnalyticsService collaborationService,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsInsightService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<Common.ServiceResult<InsightReport>> GetAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var overviewResult = await overviewService.GetAsync(query, cancellationToken);

        if (!overviewResult.IsSuccess)
        {
            return Common.ServiceResult<InsightReport>.Fail(
                overviewResult.StatusCode,
                overviewResult.Error!);
        }

        var overview = overviewResult.Value!;
        var report = await cache.GetOrCreateAsync(
            Caching.CacheKeys.Analytics(
                "insights",
                overview.Range.CacheTag,
                AnalyticsCacheTags.Filter(query.DepartmentId),
                AnalyticsCacheTags.InstantBucket(overview.Range.AsOf)),
            _options.InsightTtl,
            token => BuildAsync(query, overview, token),
            cancellationToken);

        return Common.ServiceResult<InsightReport>.Ok(report);
    }

    private async Task<InsightReport> BuildAsync(
        AnalyticsQuery query,
        PlatformOverview overview,
        CancellationToken cancellationToken)
    {
        var insights = new List<Insight>();
        var population = await populationService.GetAsync(cancellationToken);

        var departments = await departmentService.GetAsync(query, cancellationToken);
        var collaboration = await collaborationService.GetThanksAsync(query, cancellationToken);

        AddFormatInsights(insights, overview);
        AddDepartmentInsights(insights, departments.IsSuccess ? departments.Value! : null);
        AddSilentEmployeesInsight(insights, overview, population);
        AddDataQualityInsight(insights, population);
        AddPointsInsight(insights, overview);
        AddCollaborationInsight(insights, collaboration.IsSuccess ? collaboration.Value! : null);
        await AddRewardInsightsAsync(insights, overview, cancellationToken);
        await AddQuestStepInsightsAsync(insights, overview, cancellationToken);

        var ordered = insights
            .OrderBy(insight => SeverityRank(insight.Severity))
            .ThenBy(insight => insight.Code, StringComparer.Ordinal)
            .ToList();

        return new InsightReport(ordered, overview.Range, DateTime.UtcNow);
    }

    private void AddFormatInsights(List<Insight> insights, PlatformOverview overview)
    {
        var reliable = overview.Formats.Where(format => format.IsReliable).ToList();

        foreach (var format in reliable)
        {
            if (format.CompletionRate is not double completion || format.Participants < _options.MinCohort)
            {
                continue;
            }

            if (format.Format == "quest" && completion < 0.3d)
            {
                insights.Add(new Insight(
                    "QUEST_LOW_COMPLETION",
                    "warning",
                    $"Низкая доля завершения заданий: {Math.Round(completion * 100)}%",
                    $"Из {format.Participants} участников задания завершили {Math.Round(completion * 100)}%.",
                    new Dictionary<string, string>
                    {
                        ["completionRate"] = Percent(completion),
                        ["participants"] = format.Participants.ToString(),
                        ["threshold"] = "30%"
                    },
                    "Проверьте обязательные шаги и требования к доказательствам: возможно, задание слишком длинное.",
                    null,
                    null));
            }

            if (format.Format == "event" && completion < 0.5d)
            {
                insights.Add(new Insight(
                    "EVENT_LOW_TURNUP",
                    "warning",
                    $"Низкая явка на мероприятия: {Math.Round(completion * 100)}%",
                    $"Доля посещения (посетили / посетили+неявки) составила {Math.Round(completion * 100)}%.",
                    new Dictionary<string, string>
                    {
                        ["attendanceRate"] = Percent(completion),
                        ["activities"] = format.Activities.ToString(),
                        ["threshold"] = "50%"
                    },
                    "Проверьте время проведения и напоминания участникам.",
                    null,
                    null));
            }

            if (format.Format == "survey" && completion < 0.25d)
            {
                insights.Add(new Insight(
                    "SURVEY_LOW_RESPONSE",
                    "warning",
                    $"Низкая доля ответов на опросы: {Math.Round(completion * 100)}%",
                    "Доля ответивших от активных сотрудников ниже 25%.",
                    new Dictionary<string, string>
                    {
                        ["responseRate"] = Percent(completion),
                        ["respondents"] = format.Participants.ToString(),
                        ["threshold"] = "25%"
                    },
                    "Сократите опрос или пересмотрите размер награды.",
                    null,
                    null));
            }

            if (format.RewardClaimRate is double claim && format.Format == "event" && claim < 0.5d)
            {
                insights.Add(new Insight(
                    "REWARD_UNCLAIMED",
                    "info",
                    $"Половина наград за мероприятия не получена: {Math.Round(claim * 100)}%",
                    "Награда за мероприятие начисляется только после явного запроса сотрудника.",
                    new Dictionary<string, string>
                    {
                        ["rewardClaimRate"] = Percent(claim),
                        ["threshold"] = "50%"
                    },
                    "Напомните участникам о награде или рассмотрите автоматическое начисление.",
                    null,
                    null));
            }
        }

        var bestReach = reliable.OrderByDescending(format => format.ReachRate ?? 0d).FirstOrDefault();
        var worstReach = reliable.OrderBy(format => format.ReachRate ?? 0d).FirstOrDefault();

        if (bestReach is not null
            && worstReach is not null
            && bestReach.Format != worstReach.Format
            && bestReach.ReachRate is double best
            && worstReach.ReachRate is double worst
            && best > 0
            && worst < best * 0.5d)
        {
            insights.Add(new Insight(
                "FORMAT_UNDERPERFORMS",
                "info",
                $"Формат «{worstReach.Title}» охватывает вдвое меньше сотрудников",
                $"Охват «{worstReach.Title}» — {Percent(worst)}, лучший формат «{bestReach.Title}» — {Percent(best)}.",
                new Dictionary<string, string>
                {
                    ["worstFormat"] = worstReach.Format,
                    ["worstReach"] = Percent(worst),
                    ["bestFormat"] = bestReach.Format,
                    ["bestReach"] = Percent(best)
                },
                "Сравните сложность и видимость форматов в интерфейсе сотрудника.",
                null,
                null));
        }
    }

    private void AddDepartmentInsights(
        List<Insight> insights,
        DepartmentAnalyticsResult? departments)
    {
        if (departments is null)
        {
            return;
        }

        var reliable = departments.Rows.Where(row => row.IsReliable).ToList();

        if (reliable.Count < 2)
        {
            return;
        }

        var median = AnalyticsMetrics.Median(
            reliable.Where(row => row.ActivityRate is not null).Select(row => row.ActivityRate!.Value));

        if (median is double medianRate && medianRate > 0)
        {
            foreach (var row in reliable.Where(row =>
                         row.ActivityRate is double rate && rate < medianRate * 0.5d))
            {
                insights.Add(new Insight(
                    "DEPT_ACTIVITY_GAP",
                    "warning",
                    $"Отдел «{row.Department}» участвует заметно реже остальных",
                    $"Доля участия {Percent(row.ActivityRate!.Value)} против медианы {Percent(medianRate)}.",
                    new Dictionary<string, string>
                    {
                        ["activityRate"] = Percent(row.ActivityRate!.Value),
                        ["medianActivityRate"] = Percent(medianRate),
                        ["headcount"] = row.Headcount.ToString()
                    },
                    "Уточните, знают ли сотрудники отдела о доступных активностях.",
                    "department",
                    row.DepartmentId));
            }
        }

        foreach (var row in reliable.Where(row =>
                     row.EngagementChange is double change && change <= -10d))
        {
            insights.Add(new Insight(
                "DEPT_ENGAGEMENT_DECLINE",
                "warning",
                $"Вовлечённость отдела «{row.Department}» снижается",
                $"Средний балл изменился на {row.EngagementChange:+0.0;-0.0} пункта к предыдущему периоду.",
                new Dictionary<string, string>
                {
                    ["engagementScore"] = row.EngagementScore?.ToString("0.0") ?? "—",
                    ["engagementChange"] = row.EngagementChange?.ToString("+0.0;-0.0") ?? "—",
                    ["threshold"] = "-10.0"
                },
                "Обсудите с руководителем отдела, что изменилось в доступных активностях.",
                "department",
                row.DepartmentId));
        }
    }

    private void AddSilentEmployeesInsight(
        List<Insight> insights,
        PlatformOverview overview,
        AnalyticsPopulation population)
    {
        var participants = overview.Participation
            .FirstOrDefault(metric => metric.Code == "participants")?.Numerator ?? 0;

        var silent = Math.Max(population.ActiveUsers - participants, 0);

        if (population.ActiveUsers < _options.MinCohort || silent == 0)
        {
            return;
        }

        var share = (double)silent / population.ActiveUsers;

        if (share < 0.25d)
        {
            return;
        }

        insights.Add(new Insight(
            "SILENT_EMPLOYEES",
            "warning",
            $"Без активности: {Math.Round(share * 100)}% активных сотрудников",
            $"{silent} из {population.ActiveUsers} активных сотрудников не совершили ни одного действия за окно.",
            new Dictionary<string, string>
            {
                ["silentEmployees"] = silent.ToString(),
                ["activeUsers"] = population.ActiveUsers.ToString(),
                ["share"] = Percent(share)
            },
            "Проверьте доступность платформы и напомните о ближайших активностях.",
            null,
            null));
    }

    private void AddDataQualityInsight(List<Insight> insights, AnalyticsPopulation population)
    {
        if (population.ActiveUsers < _options.MinCohort || population.ActiveWithoutDepartment == 0)
        {
            return;
        }

        var share = (double)population.ActiveWithoutDepartment / population.ActiveUsers;

        if (share < 0.05d)
        {
            return;
        }

        insights.Add(new Insight(
            "DATA_QUALITY_DEPARTMENTS",
            "info",
            $"Без отдела: {population.ActiveWithoutDepartment} сотрудников",
            "Сотрудники без отдела не попадают в сравнение отделов и учитываются отдельной строкой.",
            new Dictionary<string, string>
            {
                ["withoutDepartment"] = population.ActiveWithoutDepartment.ToString(),
                ["share"] = Percent(share)
            },
            "Заполните отдел в карточках сотрудников, чтобы сравнение отделов было полным.",
            null,
            null));
    }

    private static void AddPointsInsight(List<Insight> insights, PlatformOverview overview)
    {
        long Metric(string code) =>
            overview.Economy.FirstOrDefault(metric => metric.Code == code)?.Numerator ?? 0;

        var earned = Metric("points_earned");
        var spent = Metric("points_spent");
        var outstanding = Metric("points_outstanding_personal");

        if (earned <= 0 || outstanding <= earned * 3)
        {
            return;
        }

        insights.Add(new Insight(
            "POINTS_LIABILITY",
            "warning",
            "Накопленные баллы заметно превышают начисления за период",
            "Остаток на личных балансах более чем втрое превышает начисленное за окно: спрос на каталог может быть ниже предложения.",
            new Dictionary<string, string>
            {
                ["pointsEarned"] = earned.ToString(),
                ["pointsSpent"] = spent.ToString(),
                ["pointsOutstanding"] = outstanding.ToString()
            },
            "Пересмотрите ассортимент каталога или условия начисления баллов.",
            null,
            null));
    }

    private void AddCollaborationInsight(
        List<Insight> insights,
        CollaborationThanksResult? collaboration)
    {
        if (collaboration?.CrossDepartmentShare is not double share || collaboration.Total < _options.MinCohort)
        {
            return;
        }

        if (share >= 0.25d)
        {
            return;
        }

        insights.Add(new Insight(
            "SILOED_DEPARTMENT",
            "info",
            $"Редкие благодарности между отделами: {Math.Round(share * 100)}%",
            $"Только {Percent(share)} благодарностей отправлены сотрудникам других отделов.",
            new Dictionary<string, string>
            {
                ["crossDepartmentShare"] = Percent(share),
                ["thanks"] = collaboration.Total.ToString(),
                ["threshold"] = "25%"
            },
            "Поддержите межфункциональные активности, чтобы сотрудники разных отделов работали вместе.",
            null,
            null));
    }

    private async Task AddRewardInsightsAsync(
        List<Insight> insights,
        PlatformOverview overview,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && !product.IsDigital)
            .Select(product => new { product.Id, product.Name, product.Stock })
            .ToListAsync(cancellationToken);

        var demandRows = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status != OrderStatus.Cancelled
                && item.Order.Status != OrderStatus.Refunded
                && item.Order.CreatedAt >= overview.Range.From
                && item.Order.CreatedAt < overview.Range.To
                && item.ProductId != null)
            .GroupBy(item => item.ProductId!.Value)
            .Select(group => new { ProductId = group.Key, Units = group.Sum(item => (long)item.Quantity) })
            .ToListAsync(cancellationToken);

        var demand = demandRows.ToDictionary(row => row.ProductId, row => row.Units);
        var idle = products.Where(product => demand.GetValueOrDefault(product.Id) == 0).ToList();
        var outOfStockWithDemand = products
            .Where(product => product.Stock == 0 && demand.GetValueOrDefault(product.Id) > 0)
            .ToList();

        if (products.Count > 0 && idle.Count >= Math.Max(3, products.Count / 2))
        {
            insights.Add(new Insight(
                "DEAD_CATALOG",
                "info",
                $"Без спроса: {idle.Count} активных товаров",
                "Более половины активных товаров не заказывали в этом окне.",
                new Dictionary<string, string>
                {
                    ["idleProducts"] = idle.Count.ToString(),
                    ["activeProducts"] = products.Count.ToString(),
                    ["examples"] = string.Join(", ", idle.Take(3).Select(product => product.Name))
                },
                "Обновите ассортимент: замените товары без спроса.",
                null,
                null));
        }

        if (outOfStockWithDemand.Count > 0)
        {
            insights.Add(new Insight(
                "STOCKOUT_RISK",
                "warning",
                $"Нулевой остаток у {outOfStockWithDemand.Count} востребованных товаров",
                "Товары заказывали в этом окне, но остаток уже равен нулю.",
                new Dictionary<string, string>
                {
                    ["outOfStock"] = outOfStockWithDemand.Count.ToString(),
                    ["examples"] = string.Join(", ", outOfStockWithDemand.Take(3).Select(product => product.Name))
                },
                "Пополните остатки, чтобы не терять спрос.",
                "product",
                outOfStockWithDemand[0].Id));
        }

        var starvedVariants = await dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant =>
                variant.IsActive
                && variant.Stock == 0
                && dbContext.OrderItems.Any(item =>
                    item.ProductVariantId == variant.Id
                    && item.Order.CreatedAt >= overview.Range.From
                    && item.Order.CreatedAt < overview.Range.To
                    && item.Order.Status != OrderStatus.Cancelled
                    && item.Order.Status != OrderStatus.Refunded))
            .Select(variant => new
            {
                variant.Id,
                variant.Name,
                ProductName = variant.Product.Name
            })
            .Take(5)
            .ToListAsync(cancellationToken);

        if (starvedVariants.Count > 0)
        {
            insights.Add(new Insight(
                "VARIANT_STOCKOUT_RISK",
                "warning",
                $"Нулевой остаток у {starvedVariants.Count} востребованных вариантов",
                "Варианты заказывали в этом окне, но их остаток равен нулю.",
                new Dictionary<string, string>
                {
                    ["outOfStock"] = starvedVariants.Count.ToString(),
                    ["examples"] = string.Join(
                        ", ",
                        starvedVariants.Take(3).Select(variant => $"{variant.ProductName} / {variant.Name}"))
                },
                "Пополните остатки вариантов: спрос подтверждён заказами.",
                "product_variant",
                starvedVariants[0].Id));
        }
    }

    private async Task AddQuestStepInsightsAsync(
        List<Insight> insights,
        PlatformOverview overview,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<QuestStepBlockRow>($"""
            WITH reaches AS (
                SELECT qs.quest_id,
                       qs.title,
                       qs.sort_order,
                       COUNT(DISTINCT qp.user_id)::bigint AS "Reached"
                FROM public.quest_steps qs
                LEFT JOIN public.quest_step_completions qsc ON qsc.step_id = qs.id
                LEFT JOIN public.quest_participations qp ON qp.id = qsc.participation_id
                WHERE qs.quest_id IN (
                    SELECT q.id FROM public.quests q
                    WHERE q.is_active
                      AND q.created_at < {overview.Range.To}
                      AND (q.end_at IS NULL OR q.end_at >= {overview.Range.From})
                )
                GROUP BY qs.quest_id, qs.title, qs.sort_order
            ),
            ordered AS (
                SELECT quest_id, title, sort_order, "Reached",
                       LAG("Reached") OVER (PARTITION BY quest_id ORDER BY sort_order) AS "Previous"
                FROM reaches
            )
            SELECT o.quest_id AS "QuestId",
                   o.title AS "Title",
                   o."Reached" AS "Reached",
                   o."Previous" AS "Previous"
            FROM ordered o
            WHERE o."Previous" >= {_options.MinCohort}
              AND o."Reached" < o."Previous" * 0.5
            ORDER BY (o."Previous" - o."Reached") DESC
            LIMIT 5
            """).ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var drop = row.Previous - row.Reached;

            insights.Add(new Insight(
                "QUEST_STEP_BLOCKED",
                "warning",
                $"Шаг «{row.Title}» теряет участников",
                $"До шага доходило {row.Previous} участников, после него осталось {row.Reached}.",
                new Dictionary<string, string>
                {
                    ["questId"] = row.QuestId.ToString(),
                    ["step"] = row.Title,
                    ["before"] = row.Previous.ToString(),
                    ["after"] = row.Reached.ToString(),
                    ["lost"] = drop.ToString()
                },
                "Проверьте требования шага и условия загрузки доказательств.",
                "quest",
                row.QuestId));
        }
    }

    private static string Percent(double value) => Math.Round(value * 100, 1).ToString("0.#") + "%";

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2
    };

    /// <summary>Строка наблюдения по шагу задания.</summary>
    internal sealed class QuestStepBlockRow
    {
        /// <summary>Идентификатор задания.</summary>
        public long QuestId { get; set; }

        /// <summary>Название шага.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Дошло до шага.</summary>
        public long Reached { get; set; }

        /// <summary>Дошло до предыдущего шага.</summary>
        public long Previous { get; set; }
    }
}
