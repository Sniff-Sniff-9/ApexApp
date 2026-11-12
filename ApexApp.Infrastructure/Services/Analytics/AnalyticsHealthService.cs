using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class AnalyticsHealthService(
    AppDbContext dbContext,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsHealthService
{
    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<AnalyticsHealth>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var health = await cache.GetOrCreateAsync(
            CacheKeys.Analytics("health", "-", "-", AnalyticsCacheTags.InstantBucket(DateTime.UtcNow)),
            _options.HealthTtl,
            LoadAsync,
            cancellationToken);

        return ServiceResult<AnalyticsHealth>.Ok(health);
    }

    private async Task<AnalyticsHealth> LoadAsync(CancellationToken cancellationToken)
    {
        var asOf = DateTime.UtcNow;
        var population = await populationService.GetAsync(cancellationToken);
        var checks = new List<AnalyticsHealthCheck>();

        checks.Add(new AnalyticsHealthCheck(
            "headcount",
            "Активные сотрудники",
            "info",
            population.ActiveUsers,
            population.TotalUsers,
            "Численность отражает текущий статус учётных записей."));

        if (population.ActiveWithoutDepartment > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "users_without_department",
                "Активные сотрудники без отдела",
                Share(population.ActiveWithoutDepartment, population.ActiveUsers) >= 0.05d ? "warning" : "info",
                population.ActiveWithoutDepartment,
                population.ActiveUsers,
                "Такие сотрудники не попадают в сравнение отделов."));
        }

        var neverLoggedIn = await dbContext.Users
            .AsNoTracking()
            .LongCountAsync(
                user => user.IsActive && user.LastLoginAt == null,
                cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "users_never_logged_in",
            "Ни разу не входили",
            neverLoggedIn > 0 ? "warning" : "info",
            neverLoggedIn,
            population.ActiveUsers,
            "Истории входов нет: учитывается только последний вход."));

        var activeProducts = await dbContext.Products
            .AsNoTracking()
            .LongCountAsync(product => product.IsActive, cancellationToken);

        var outOfStock = await dbContext.Products
            .AsNoTracking()
            .LongCountAsync(product => product.IsActive && product.Stock == 0, cancellationToken);

        var unlimitedStock = await dbContext.Products
            .AsNoTracking()
            .LongCountAsync(product => product.IsActive && product.Stock == null, cancellationToken);

        var digital = await dbContext.Products
            .AsNoTracking()
            .LongCountAsync(product => product.IsActive && product.IsDigital, cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "products",
            "Активные товары",
            "info",
            activeProducts,
            null,
            $"Без остатка: {outOfStock}; без учёта остатка: {unlimitedStock}; цифровых: {digital}."));

        if (digital > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "digital_products_unorderable",
                "Цифровые товары недоступны для заказа",
                "warning",
                digital,
                activeProducts,
                "Оформление заказа отклоняет цифровые товары, поэтому спрос на них не измеряется."));
        }

        var variants = await dbContext.ProductVariants
            .AsNoTracking()
            .GroupBy(variant => new { variant.IsActive, IsOutOfStock = variant.Stock == 0 })
            .Select(group => new
            {
                group.Key.IsActive,
                group.Key.IsOutOfStock,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var activeVariants = variants
            .Where(row => row.IsActive)
            .Sum(row => (long)row.Count);

        var outOfStockVariants = variants
            .Where(row => row.IsActive && row.IsOutOfStock)
            .Sum(row => (long)row.Count);

        if (activeVariants > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "product_variants",
                "Активные варианты товаров",
                outOfStockVariants > 0 ? "info" : "info",
                activeVariants,
                outOfStockVariants,
                $"Без остатка: {outOfStockVariants}. Варианты продаются отдельно от товара."));
        }

        var orphanOrderItems = await dbContext.OrderItems
            .AsNoTracking()
            .LongCountAsync(item => item.ProductId == null, cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "orphan_order_items",
            "Строки заказов удалённых товаров",
            orphanOrderItems > 0 ? "warning" : "info",
            orphanOrderItems,
            null,
            "Товар удалён из каталога: спрос учитывается отдельной строкой."));

        var pendingOrders = await dbContext.Orders
            .AsNoTracking()
            .LongCountAsync(order => order.Status == OrderStatus.Pending, cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "orders_pending",
            "Заказы в статусе «ожидает оплаты»",
            pendingOrders > 0 ? "warning" : "info",
            pendingOrders,
            null,
            "Оформление заказа сразу переводит его в «оплачен»: статус «ожидает» остаётся у старых записей."));

        var expiredEvents = await dbContext.Events
            .AsNoTracking()
            .LongCountAsync(
                eventItem => eventItem.IsActive && eventItem.EndAt < asOf,
                cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "events_expired_active",
            "Завершённые, но активные мероприятия",
            expiredEvents > 0 ? "info" : "info",
            expiredEvents,
            null,
            "Активность мероприятия не влияет на историю участия."));

        var surveysWithoutResponses = await dbContext.Surveys
            .AsNoTracking()
            .LongCountAsync(
                survey => survey.IsActive
                          && (survey.EndAt == null || survey.EndAt >= asOf)
                          && !dbContext.SurveyResponses.Any(response => response.SurveyId == survey.Id),
                cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "surveys_without_responses",
            "Активные опросы без ответов",
            surveysWithoutResponses > 0 ? "warning" : "info",
            surveysWithoutResponses,
            null,
            "Опрос опубликован, но ни один сотрудник не ответил."));

        var questsWithoutParticipants = await dbContext.Quests
            .AsNoTracking()
            .LongCountAsync(
                quest => quest.IsActive
                         && (quest.EndAt == null || quest.EndAt >= asOf)
                         && !dbContext.QuestParticipations.Any(participation => participation.QuestId == quest.Id),
                cancellationToken);

        checks.Add(new AnalyticsHealthCheck(
            "quests_without_participants",
            "Активные задания без участников",
            questsWithoutParticipants > 0 ? "warning" : "info",
            questsWithoutParticipants,
            null,
            "Задание опубликовано, но к нему никто не присоединился."));

        var selfThanks = await dbContext.Thanks
            .AsNoTracking()
            .LongCountAsync(thank => thank.FromUserId == thank.ToUserId, cancellationToken);

        if (selfThanks > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "thanks_self",
                "Самоблагодарности",
                "warning",
                selfThanks,
                null,
                "Благодарность самому себе искажает показатели признания."));
        }

        var duplicateResponses = await dbContext.SurveyResponses
            .AsNoTracking()
            .GroupBy(response => new { response.SurveyId, response.UserId })
            .Where(group => group.Count() > 1)
            .CountAsync(cancellationToken);

        if (duplicateResponses > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "duplicate_survey_responses",
                "Повторные ответы на один опрос",
                "info",
                duplicateResponses,
                null,
                "Ответы учитываются как отдельные, а респонденты — как уникальные сотрудники."));
        }

        var usersWithoutBalance = await dbContext.Users
            .AsNoTracking()
            .LongCountAsync(
                user => user.IsActive
                        && !dbContext.Balances.Any(balance =>
                            balance.UserId == user.Id && balance.Type == BalanceType.Personal),
                cancellationToken);

        if (usersWithoutBalance > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "users_without_balance",
                "Активные сотрудники без личного баланса",
                "info",
                usersWithoutBalance,
                population.ActiveUsers,
                "Баланс создаётся при первом начислении или списании."));
        }

        var emptyDepartments = await dbContext.Departments
            .AsNoTracking()
            .LongCountAsync(
                department => department.IsActive
                              && !dbContext.Users.Any(user =>
                                  user.DepartmentId == department.Id && user.IsActive),
                cancellationToken);

        if (emptyDepartments > 0)
        {
            checks.Add(new AnalyticsHealthCheck(
                "departments_without_employees",
                "Активные отделы без сотрудников",
                "info",
                emptyDepartments,
                null,
                "Отдел не участвует в сравнении, пока в нём нет активных сотрудников."));
        }

        var caveats = new List<string>(3)
        {
            "Проверки отражают текущее состояние данных и не заменяют сверку с внешними системами.",
            "Истории изменения статусов и отделов нет: часть показателей невозможно восстановить за прошлые периоды.",
            "Цифровые товары не участвуют в заказах, поэтому их спрос не измеряется."
        };

        if (population.ActiveUsers < _options.MinCohort)
        {
            caveats.Add(
                $"Активных сотрудников меньше {_options.MinCohort}: доли и рейтинги скрываются как недостоверные.");
        }

        return new AnalyticsHealth(
            checks
                .OrderBy(check => SeverityRank(check.Severity))
                .ThenBy(check => check.Code, StringComparer.Ordinal)
                .ToList(),
            asOf,
            caveats);
    }

    private static double Share(long value, long total) => total <= 0 ? 0d : (double)value / total;

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2
    };
}
