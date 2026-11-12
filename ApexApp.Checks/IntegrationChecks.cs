using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Services;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexApp.Checks;

/// <summary>
/// Runs every analytics service against the real PostgreSQL schema and verifies
/// cross-service invariants. Query translation errors surface here, which is the
/// main risk that unit checks cannot cover.
/// </summary>
internal static class IntegrationChecks
{
    public static async Task<ServiceProvider> BuildProviderAsync(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.Configure<AnalyticsOptions>(options =>
        {
            options.TimeZone = "Europe/Moscow";
            options.DefaultRangeDays = 30;
            options.MaxRangeDays = 400;
            options.MaxBuckets = 62;
            options.MinCohort = 5;
            options.EngagementTrendPeriods = 2;
            options.MaxEngagementTrendPeriods = 4;
        });

        services.AddSingleton<ICacheService, SimpleCache>();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MapEnum<BalanceType>("balance_type");
            npgsql.MapEnum<TransactionType>("transaction_type");
            npgsql.MapEnum<OrderStatus>("order_status");
            npgsql.MapEnum<TicketStatus>("ticket_status");
            npgsql.MapEnum<QuestStepCompletionType>("quest_step_completion_type");
        }));

        services.AddScoped<AnalyticsCache>();
        services.AddSingleton<IAnalyticsRangeFactory, AnalyticsRangeFactory>();
        services.AddScoped<IAnalyticsPopulationService, AnalyticsPopulationService>();
        services.AddScoped<IAnalyticsActivityRollupService, AnalyticsActivityRollupService>();
        services.AddScoped<IAnalyticsActivityAggregates, AnalyticsActivityAggregates>();
        services.AddScoped<IAnalyticsEngagementLoader, AnalyticsEngagementLoader>();
        services.AddScoped<IAnalyticsOverviewService, AnalyticsOverviewService>();
        services.AddScoped<IAnalyticsSeriesService, AnalyticsSeriesService>();
        services.AddScoped<IActivityAnalyticsService, ActivityAnalyticsService>();
        services.AddScoped<IRewardAnalyticsService, RewardAnalyticsService>();
        services.AddScoped<IEngagementAnalyticsService, EngagementAnalyticsService>();
        services.AddScoped<IDepartmentAnalyticsService, DepartmentAnalyticsService>();
        services.AddScoped<ICollaborationAnalyticsService, CollaborationAnalyticsService>();
        services.AddScoped<IAnalyticsInsightService, AnalyticsInsightService>();
        services.AddScoped<IAnalyticsHealthService, AnalyticsHealthService>();

        // Сервисы оформления заказа: нужны для сквозной проверки корзины и checkout.
        services.AddScoped<CartRepository>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IOrderService, OrderService>();

        var provider = services.BuildServiceProvider();

        // Fail fast with a clear message when the database is not reachable.
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");

        return provider;
    }

    public static async Task RunAsync(CheckRunner runner, ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var query = new AnalyticsQuery(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            AnalyticsGranularity.Day,
            null,
            10);

        var overviewService = services.GetRequiredService<IAnalyticsOverviewService>();
        var seriesService = services.GetRequiredService<IAnalyticsSeriesService>();
        var activityService = services.GetRequiredService<IActivityAnalyticsService>();
        var rewardService = services.GetRequiredService<IRewardAnalyticsService>();
        var engagementService = services.GetRequiredService<IEngagementAnalyticsService>();
        var departmentService = services.GetRequiredService<IDepartmentAnalyticsService>();
        var collaborationService = services.GetRequiredService<ICollaborationAnalyticsService>();
        var insightService = services.GetRequiredService<IAnalyticsInsightService>();
        var healthService = services.GetRequiredService<IAnalyticsHealthService>();
        var populationService = services.GetRequiredService<IAnalyticsPopulationService>();

        PlatformOverview? overview = null;

        await runner.CheckAsync("overview.returns-data", async () =>
        {
            var result = await overviewService.GetAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"overview failed: {result.Error}");

            overview = result.Value!;

            CheckRunner.Ensure(overview.Formats.Count == 3, "three activity formats expected");
            CheckRunner.Ensure(overview.Range.From < overview.Range.To, "range must be ordered");
            CheckRunner.Ensure(overview.Headcount.Count >= 4, "headcount metrics expected");
            CheckRunner.Ensure(overview.Deltas.Count >= 5, "deltas expected");
            CheckRunner.Ensure(overview.Caveats.Count > 0, "caveats must be present");

            var participants = overview.Participation.First(metric => metric.Code == "participants");
            var rate = overview.Participation.First(metric => metric.Code == "participation_rate");

            CheckRunner.Ensure(participants.Numerator >= 0, "participants must be non-negative");
            CheckRunner.Ensure(rate.IsReliable == (rate.Denominator >= 5), "rate reliability must follow the cohort rule");
        });

        await runner.CheckAsync("overview.department-scope", async () =>
        {
            var population = await populationService.GetAsync();
            var departmentId = population.ActiveByDepartment.Keys.FirstOrDefault();

            if (departmentId == 0)
            {
                return;
            }

            var result = await overviewService.GetAsync(query with { DepartmentId = departmentId });

            CheckRunner.Ensure(result.IsSuccess, $"department overview failed: {result.Error}");

            var active = result.Value!.Headcount.First(metric => metric.Code == "active_users");
            var expected = population.ActiveByDepartment[departmentId];

            CheckRunner.Ensure(active.Numerator == expected, $"headcount must equal {expected}");
        });

        await runner.CheckAsync("series.participants", async () =>
        {
            var result = await seriesService.GetAsync(query, AnalyticsSeriesMetric.Participants);

            CheckRunner.Ensure(result.IsSuccess, $"series failed: {result.Error}");

            var points = result.Value!.Points;

            CheckRunner.Ensure(points.Count > 0, "series must contain points");
            CheckRunner.Ensure(points.All(point => point.Value >= 0 && point.UniqueUsers >= 0), "values must be non-negative");
            CheckRunner.Ensure(points.Zip(points.Skip(1)).All(pair => pair.First.PeriodStart < pair.Second.PeriodStart), "points ordered");
        });

        await runner.CheckAsync("series.weekly-and-monthly", async () =>
        {
            var weekly = await seriesService.GetAsync(
                query with { Granularity = AnalyticsGranularity.Week },
                AnalyticsSeriesMetric.Actions);

            var monthly = await seriesService.GetAsync(
                query with { Granularity = AnalyticsGranularity.Month },
                AnalyticsSeriesMetric.PointsEarned);

            CheckRunner.Ensure(weekly.IsSuccess, $"weekly series failed: {weekly.Error}");
            CheckRunner.Ensure(monthly.IsSuccess, $"monthly series failed: {monthly.Error}");
            CheckRunner.Ensure(weekly.Value!.Points.Count <= 62, "weekly bucket cap");
            CheckRunner.Ensure(monthly.Value!.Points.Count <= 62, "monthly bucket cap");
        });

        await runner.CheckAsync("series.orders-and-spent", async () =>
        {
            var orders = await seriesService.GetAsync(query, AnalyticsSeriesMetric.Orders);
            var spent = await seriesService.GetAsync(query, AnalyticsSeriesMetric.PointsSpent);

            CheckRunner.Ensure(orders.IsSuccess, $"orders series failed: {orders.Error}");
            CheckRunner.Ensure(spent.IsSuccess, $"spent series failed: {spent.Error}");
        });

        await runner.CheckAsync("activities.list", async () =>
        {
            var result = await activityService.GetActivitiesAsync(query, null, ActivitySortBy.Participants);

            CheckRunner.Ensure(result.IsSuccess, $"activity list failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.TotalCount >= result.Value.Items.Count, "total must cover the page");
            CheckRunner.Ensure(result.Value.Items.Count <= 10, "top must be honoured");
        });

        await runner.CheckAsync("activities.detail-per-format", async () =>
        {
            foreach (var format in new[] { "event", "quest", "survey" })
            {
                var list = await activityService.GetActivitiesAsync(query, format, ActivitySortBy.Participants);
                CheckRunner.Ensure(list.IsSuccess, $"{format} list failed: {list.Error}");

                var first = list.Value!.Items.FirstOrDefault();

                if (first is null)
                {
                    continue;
                }

                var detail = await activityService.GetDetailAsync(query, format, first.Id, cancellationToken: default);

                CheckRunner.Ensure(detail.IsSuccess, $"{format} detail failed: {detail.Error}");
                CheckRunner.Ensure(detail.Value!.Funnel.Count > 0, "funnel must not be empty");
                CheckRunner.Ensure(detail.Value.Series.Count > 0, "activity series must not be empty");

                if (format == "quest")
                {
                    CheckRunner.Ensure(detail.Value.Metrics.Count >= 5, "quest metrics expected");
                }
            }
        });

        await runner.CheckAsync("activities.unknown-format", async () =>
        {
            var result = await activityService.GetActivitiesAsync(query, "webinar", ActivitySortBy.Participants);
            CheckRunner.Ensure(!result.IsSuccess && result.StatusCode == 400, "400 expected for unknown format");

            var detail = await activityService.GetDetailAsync(query, "quest", 999_999_999);
            CheckRunner.Ensure(!detail.IsSuccess && detail.StatusCode == 404, "404 expected for missing activity");
        });

        await runner.CheckAsync("rewards.catalog", async () =>
        {
            var result = await rewardService.GetCatalogAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"catalog failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.Items.Count > 0, "catalog must not be empty");
            CheckRunner.Ensure(result.Value.TotalCount >= result.Value.Items.Count, "total must cover the page");
        });

        await runner.CheckAsync("rewards.categories", async () =>
        {
            var result = await rewardService.GetCategoriesAsync(query);
            CheckRunner.Ensure(result.IsSuccess, $"categories failed: {result.Error}");
        });

        await runner.CheckAsync("rewards.economy", async () =>
        {
            var result = await rewardService.GetEconomyAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"economy failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.EarnedByType.Count > 0, "earnings by type expected");

            if (overview is not null)
            {
                var earned = overview.Economy.First(metric => metric.Code == "points_earned").Numerator;

                CheckRunner.Ensure(
                    earned == result.Value.EarnedTotal,
                    $"overview points_earned ({earned}) must equal economy total ({result.Value.EarnedTotal})");
            }
        });

        await runner.CheckAsync("rewards.orders-consistent-with-overview", async () =>
        {
            var result = await rewardService.GetOrdersAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"orders failed: {result.Error}");

            if (overview is not null)
            {
                var orders = overview.Economy.First(metric => metric.Code == "orders").Numerator;

                CheckRunner.Ensure(
                    orders == result.Value!.Orders,
                    $"overview orders ({orders}) must equal order analytics ({result.Value.Orders})");
            }
        });

        await runner.CheckAsync("rewards.variants", async () =>
        {
            var result = await rewardService.GetVariantsAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"variant analytics failed: {result.Error}");

            var dbContext = services.GetRequiredService<AppDbContext>();
            var expected = await dbContext.ProductVariants.CountAsync();

            Console.WriteLine($"       productVariants={expected}");

            CheckRunner.Ensure(
                result.Value!.TotalCount == expected,
                $"variant total ({result.Value.TotalCount}) must equal table count ({expected})");
            CheckRunner.Ensure(
                result.Value.Items.All(row => row.VariantId > 0 && row.Stock >= 0 && row.Units >= 0),
                "variant rows must be well-formed");
            CheckRunner.Ensure(
                result.Value.Caveats.Count > 0,
                "variant caveats expected");
        });

        await runner.CheckAsync("cart-and-order-variant-reads", async () =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();

            // Проверяется, что запросы с навигацией на вариант выполняются на реальной схеме.
            var cartRows = await dbContext.CartItems
                .AsNoTracking()
                .Include(item => item.ProductVariant)
                .Take(5)
                .ToListAsync();

            var orderItemsWithVariant = await dbContext.OrderItems
                .AsNoTracking()
                .CountAsync(item => item.ProductVariantId != null);

            var historyRows = await dbContext.Orders
                .AsNoTracking()
                .Include(order => order.OrderItems)
                .ThenInclude(item => item.ProductVariant)
                .Take(5)
                .ToListAsync();

            Console.WriteLine(
                $"       cartRows={cartRows.Count} orderItemsWithVariant={orderItemsWithVariant} " +
                $"orders={historyRows.Count}");

            CheckRunner.Ensure(
                historyRows.All(order => order.OrderItems.All(item =>
                    item.ProductVariantId is null || item.ProductVariant is not null)),
                "variant navigation must resolve for order items");
        });

        await runner.CheckAsync("no-test-residue", async () =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();

            var products = await dbContext.Products
                .CountAsync(product => product.Name.StartsWith("AUTOCHECK-"));

            CheckRunner.Ensure(products == 0, $"temporary products must not remain, found {products}");
        });

        await runner.CheckAsync("engagement.trend", async () =>
        {
            var result = await engagementService.GetTrendAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"engagement trend failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.Points.Count == 2, "two configured periods expected");

            foreach (var point in result.Value.Points)
            {
                CheckRunner.Ensure(point.Tiers.Count == 5, "five tiers expected");
                CheckRunner.Ensure(point.Headcount >= 0, "headcount must be non-negative");

                if (point.AverageScore is double average)
                {
                    CheckRunner.Ensure(average is >= 0d and <= 100d, "average must be within 0..100");
                }
            }
        });

        await runner.CheckAsync("engagement.declining", async () =>
        {
            var result = await engagementService.GetDecliningAsync(query, includeNames: true);

            CheckRunner.Ensure(result.IsSuccess, $"declining failed: {result.Error}");

            foreach (var employee in result.Value!.Employees)
            {
                CheckRunner.Ensure(employee.Change <= 0d, "declining employees must have non-positive change");
                CheckRunner.Ensure(!string.IsNullOrWhiteSpace(employee.EmployeeRef), "reference required");
            }
        });

        await runner.CheckAsync("departments.list", async () =>
        {
            var result = await departmentService.GetAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"departments failed: {result.Error}");

            var rows = result.Value!.Rows;

            CheckRunner.Ensure(rows.Count > 0, "departments must not be empty");

            var population = await populationService.GetAsync();
            var headcount = rows.Sum(row => row.Headcount);

            CheckRunner.Ensure(
                headcount == population.ActiveUsers,
                $"department headcounts ({headcount}) must cover active users ({population.ActiveUsers})");
        });

        await runner.CheckAsync("departments.detail", async () =>
        {
            var list = await departmentService.GetAsync(query);
            var departmentId = list.Value!.Rows.FirstOrDefault(row => row.DepartmentId is not null)?.DepartmentId;

            if (departmentId is null)
            {
                return;
            }

            var detail = await departmentService.GetDetailAsync(query, departmentId.Value);

            CheckRunner.Ensure(detail.IsSuccess, $"department detail failed: {detail.Error}");
            CheckRunner.Ensure(detail.Value!.Series.Points.Count > 0, "department series expected");
        });

        await runner.CheckAsync("collaboration.thanks", async () =>
        {
            var result = await collaborationService.GetThanksAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"collaboration failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.CrossDepartment <= result.Value.Total, "cross cannot exceed total");
            CheckRunner.Ensure(
                result.Value.Series.Sum(point => point.Total) == result.Value.Total,
                "series total must equal the range total");
        });

        await runner.CheckAsync("insights", async () =>
        {
            var result = await insightService.GetAsync(query);

            CheckRunner.Ensure(result.IsSuccess, $"insights failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.Insights.All(insight => !string.IsNullOrWhiteSpace(insight.Code)), "codes required");
            CheckRunner.Ensure(
                result.Value.Insights.All(insight => insight.Severity is "info" or "warning" or "critical"),
                "known severities only");
        });

        await runner.CheckAsync("health", async () =>
        {
            var result = await healthService.GetAsync();

            CheckRunner.Ensure(result.IsSuccess, $"health failed: {result.Error}");
            CheckRunner.Ensure(result.Value!.Checks.Count > 0, "checks expected");
            CheckRunner.Ensure(result.Value.Caveats.Count > 0, "caveats expected");
        });
    }
}
