namespace ApexApp.Infrastructure.Caching;

/// <summary>
/// Cache keys and time to live values for the cached read models.
/// </summary>
public static class CacheKeys
{
    /// <summary>Categories list: 15 minutes.</summary>
    public static readonly TimeSpan CategoriesTtl = TimeSpan.FromMinutes(15);

    /// <summary>Achievements list: 10 minutes.</summary>
    public static readonly TimeSpan AchievementsTtl = TimeSpan.FromMinutes(10);

    /// <summary>Product catalog: 30 seconds, stock changes on checkout.</summary>
    public static readonly TimeSpan ProductsTtl = TimeSpan.FromSeconds(30);

    /// <summary>Aggregated survey results: 60 seconds.</summary>
    public static readonly TimeSpan SurveyResultsTtl = TimeSpan.FromSeconds(60);

    /// <summary>Leaderboard: 60 seconds.</summary>
    public static readonly TimeSpan LeaderboardTtl = TimeSpan.FromSeconds(60);

    /// <summary>User directory lists: 60 seconds.</summary>
    public static readonly TimeSpan UsersTtl = TimeSpan.FromSeconds(60);

    /// <summary>Roles reference list: 60 minutes.</summary>
    public static readonly TimeSpan RolesTtl = TimeSpan.FromMinutes(60);

    /// <summary>Group prefix for product catalog entries.</summary>
    public const string ProductsPrefix = "catalog:products:";

    /// <summary>Group prefix for survey result entries.</summary>
    public const string SurveyResultsPrefix = "surveys:results:";

    /// <summary>Group prefix for leaderboard entries.</summary>
    public const string LeaderboardPrefix = "leaderboard:";

    /// <summary>User engagement snapshot: 30 seconds; invalidated right after a user acts.</summary>
    public static readonly TimeSpan EngagementTtl = TimeSpan.FromSeconds(30);

    /// <summary>Group prefix for user engagement entries.</summary>
    public const string EngagementPrefix = "engagement:";

    /// <summary>Group prefix for user directory entries.</summary>
    public const string UsersPrefix = "users:";

    /// <summary>All categories.</summary>
    public const string Categories = "catalog:categories:all";

    /// <summary>All achievements.</summary>
    public const string Achievements = "achievements:all";

    /// <summary>All roles.</summary>
    public const string Roles = "roles:all";

    /// <summary>Key for the active product list, optionally filtered by category.</summary>
    /// <param name="categoryId">Category filter.</param>
    public static string Products(int? categoryId) =>
        categoryId is int id && id > 0
            ? $"{ProductsPrefix}category:{id}"
            : $"{ProductsPrefix}all";

    /// <summary>Key for a single active product.</summary>
    /// <param name="productId">Product identifier.</param>
    public static string Product(int productId) => $"{ProductsPrefix}item:{productId}";

    /// <summary>Key for a leaderboard page.</summary>
    /// <param name="days">Number of days in the ranking window.</param>
    /// <param name="limit">Maximum number of entries.</param>
    public static string Leaderboard(int days, int limit) => $"{LeaderboardPrefix}{days}:{limit}";

    /// <summary>Key for aggregated survey results.</summary>
    /// <param name="surveyId">Survey identifier.</param>
    public static string SurveyResults(long surveyId) => $"{SurveyResultsPrefix}{surveyId}";

    /// <summary>Key for the user list; the payload depends on the includeInactive flag.</summary>
    /// <param name="includeInactive">Whether inactive users are included.</param>
    public static string UsersList(bool includeInactive) =>
        includeInactive ? $"{UsersPrefix}list:all" : $"{UsersPrefix}list:active";

    /// <summary>Key for the user list of a department.</summary>
    /// <param name="department">Department name.</param>
    public static string DepartmentUsers(string department) =>
        $"{UsersPrefix}department:{department.Trim().ToLowerInvariant()}";

    /// <summary>Key for the user list of a department, keyed by identifier.</summary>
    /// <param name="departmentId">Department identifier.</param>
    public static string DepartmentUsersById(int departmentId) =>
        $"{UsersPrefix}department-id:{departmentId}";

    /// <summary>Cache key for the roles used to authorize a user; short-lived fallback.</summary>
    /// <param name="userId">User identifier.</param>
    public static string AuthRoles(Guid userId) => $"auth:roles:{userId}";

    /// <summary>Time to live for cached authorization roles.</summary>
    public static readonly TimeSpan AuthRolesTtl = TimeSpan.FromSeconds(30);

    /// <summary>Key for the engagement snapshot of a user.</summary>
    /// <param name="userId">User identifier.</param>
    public static string Engagement(Guid userId) => $"{EngagementPrefix}user:{userId}";

    /// <summary>Group prefix for every analytics cache entry.</summary>
    public const string AnalyticsPrefix = "analytics:";

    /// <summary>Key for cached analytics reference data (headcount, departments, catalog).</summary>
    /// <param name="name">Reference data name.</param>
    public static string AnalyticsReference(string name) =>
        $"{AnalyticsPrefix}ref:{name.Trim().ToLowerInvariant()}";

    /// <summary>Key for the per-user activity rollup of a range.</summary>
    /// <param name="rangeTag">Stable range tag.</param>
    public static string AnalyticsActivityRollup(string rangeTag) =>
        $"{AnalyticsPrefix}rollup:{rangeTag}";

    /// <summary>
    /// Key for an analytics projection. The scope, range, filters and the request
    /// instant bucket are all part of the key, so a manager never receives another
    /// department's cached payload.
    /// </summary>
    /// <param name="scope">Projection name.</param>
    /// <param name="rangeTag">Stable range tag.</param>
    /// <param name="filters">Normalized filter segment.</param>
    /// <param name="instantBucket">Request instant truncated to a cache bucket.</param>
    public static string Analytics(string scope, string rangeTag, string filters, string instantBucket) =>
        $"{AnalyticsPrefix}{scope.Trim().ToLowerInvariant()}:{rangeTag}:{filters}:{instantBucket}";
}