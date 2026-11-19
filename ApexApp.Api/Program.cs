using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.Caching;
using ApexApp.Api.Diagnostics;
using ApexApp.Api.Extensions;
using ApexApp.Api.Swagger;
using ApexApp.Api.Workers;
using ApexApp.Infrastructure.Auth;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Services;
using ApexApp.Infrastructure.Services.Ai;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Локальная диагностика производительности: включается только в Development
// и только при Diagnostics:Timing:Enabled=true (по умолчанию false).
var requestTimingEnabled = builder.Environment.IsRequestTimingEnabled(builder.Configuration);
var includeSqlInTimings = requestTimingEnabled &&
    builder.Configuration.GetValue<bool>($"{DiagnosticsOptions.SectionName}:IncludeSql");

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MapEnum<BalanceType>("balance_type");
            npgsql.MapEnum<TransactionType>("transaction_type");
            npgsql.MapEnum<OrderStatus>("order_status");
            npgsql.MapEnum<TicketStatus>("ticket_status");
            npgsql.MapEnum<QuestStepCompletionType>("quest_step_completion_type");

            npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

    if (requestTimingEnabled)
    {
        options.AddInterceptors(new SqlTimingInterceptor(includeSqlInTimings));
    }

    // Кеш вовлечённости сбрасывается сразу после записи действий,
    // которые участвуют в расчёте статуса.
    options.AddInterceptors(
        new EngagementCacheInvalidationInterceptor(
            serviceProvider.GetRequiredService<ICacheService>()));

});

builder.Services.AddRequestTimingDiagnostics(builder.Configuration);

// In-memory cache for shared read models (categories, catalog, achievements,
// leaderboard, survey results, user directory, roles). Keys and TTLs are
// defined in ApexApp.Infrastructure.Caching.CacheKeys; no external cache is used.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine(
    $"DefaultConnection exists: {!string.IsNullOrWhiteSpace(connectionString)}");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

    Console.WriteLine(
        $"DB Host present: {!string.IsNullOrWhiteSpace(csb.Host)}");
}

builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IBalanceRepository, BalanceRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBalanceService, BalanceService>();
builder.Services.AddScoped<IClaimsTransformation, UserClaimsTransformation>();
builder.Services.AddSingleton<IAuthorizationHandler, ActiveUserHandler>();
builder.Services.AddScoped<IThankService, ThankService>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyRoleHandler>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<CartRepository>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IEngagementService, EngagementService>();
builder.Services.AddScoped<ActivityRewardGranter>();
builder.Services.AddScoped<IActivityRewardService, ActivityRewardService>();
builder.Services.AddScoped<IQuestService, QuestService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<ProductVariantCategoryService>();
builder.Services.AddScoped<IProductDiscountService, ProductDiscountService>();
builder.Services.AddScoped<IBadgeMaintenanceService, BadgeMaintenanceService>();
builder.Services.AddScoped<IProductBadgeService, ProductBadgeService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEngagementLevelNotifier, EngagementLevelNotifier>();
builder.Services.AddScoped<ICompanyNewsService, CompanyNewsService>();
builder.Services.AddHostedService<BadgeRecalculationWorker>();

// Аналитика: настройки, кеш-обёртка и сервисы отчётов. Все сервисы только читают данные.
builder.Services.Configure<AnalyticsOptions>(
    builder.Configuration.GetSection(AnalyticsOptions.SectionName));
builder.Services.AddScoped<AnalyticsCache>();
builder.Services.AddSingleton<IAnalyticsRangeFactory, AnalyticsRangeFactory>();
builder.Services.AddScoped<IAnalyticsPopulationService, AnalyticsPopulationService>();
builder.Services.AddScoped<IAnalyticsActivityRollupService, AnalyticsActivityRollupService>();
builder.Services.AddScoped<IAnalyticsActivityAggregates, AnalyticsActivityAggregates>();
builder.Services.AddScoped<IAnalyticsEngagementLoader, AnalyticsEngagementLoader>();
builder.Services.AddScoped<IAnalyticsOverviewService, AnalyticsOverviewService>();
builder.Services.AddScoped<IAnalyticsSeriesService, AnalyticsSeriesService>();
builder.Services.AddScoped<IActivityAnalyticsService, ActivityAnalyticsService>();
builder.Services.AddScoped<IRewardAnalyticsService, RewardAnalyticsService>();
builder.Services.AddScoped<IEngagementAnalyticsService, EngagementAnalyticsService>();
builder.Services.AddScoped<IDepartmentAnalyticsService, DepartmentAnalyticsService>();
builder.Services.AddScoped<ICollaborationAnalyticsService, CollaborationAnalyticsService>();
builder.Services.AddScoped<IAnalyticsInsightService, AnalyticsInsightService>();
builder.Services.AddScoped<IAnalyticsHealthService, AnalyticsHealthService>();
builder.Services.AddScoped<IAnalyticsScopeResolver, AnalyticsScopeResolver>();

// Модуль ИИ: провайдер DeepSeek, сборка промптов и рекомендации. Ключ API задаётся
// только через User Secrets или переменные окружения и никогда не хранится в коде.
builder.Services
    .AddOptions<DeepSeekOptions>()
    .Bind(builder.Configuration.GetSection(DeepSeekOptions.SectionName))
    .PostConfigure(aiOptions =>
    {
        if (string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            aiOptions.ApiKey = builder.Configuration["DEEPSEEK_API_KEY"] ?? string.Empty;
        }
    });

builder.Services.AddHttpClient<IAiClient, DeepSeekAiClient>((serviceProvider, client) =>
{
    var aiOptions = serviceProvider.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(aiOptions.BaseUrl)
        ? "https://api.deepseek.com"
        : aiOptions.BaseUrl.TrimEnd('/');

    client.BaseAddress = new Uri(baseUrl + "/");

    // Таймаут задаётся на каждый запрос, чтобы истёкший вызов возвращал 504.
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddScoped<AiCompletionExecutor>();
builder.Services.AddScoped<AiStatisticsDigestBuilder>();
builder.Services.AddScoped<IAiContentService, AiContentService>();
builder.Services.AddScoped<IAiRecommendationService, AiRecommendationService>();

// Ограничение частоты обращений к модулю ИИ: одно окно на администратора.
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiterOptions.AddPolicy(AiRateLimiting.PolicyName, context =>
    {
        var userId = context.User.GetUserId();

        return RateLimitPartition.GetFixedWindowLimiter(
            userId?.ToString() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = AiRateLimiting.PermitLimit,
                Window = AiRateLimiting.Window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter a Supabase JWT access token."
    });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.IncludeXmlComments(xmlPath);
    options.OperationFilter<AuthorizationRequirementsOperationFilter>();
});

var supabaseUrl = builder.Configuration["Supabase:ProjectUrl"] ??
    throw new InvalidOperationException("Supabase project URL is not configured.");
var supabaseJwtJwks = builder.Configuration["Supabase:JwtJwks"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Audience = "authenticated";
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = "user_role",
            IssuerSigningKeys = string.IsNullOrWhiteSpace(supabaseJwtJwks)
                ? null
                : new JsonWebKeySet(supabaseJwtJwks).GetSigningKeys()
        };
        if (string.IsNullOrWhiteSpace(supabaseJwtJwks))
        {
            options.Authority = $"{supabaseUrl}/auth/v1";
            // The IP-based test deployment is HTTP-only; require HTTPS metadata when configured for HTTPS.
            options.RequireHttpsMetadata = !supabaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }
    });

builder.Services.AddHttpClient();

/// <summary>
/// Настройка политики авторизации для приложения. Определяются различные политики, 
/// которые проверяют аутентификацию пользователя, его активность и наличие определенных 
/// ролей для доступа к различным функциональным возможностям приложения.
/// </summary>
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.ActiveUser, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
    });

    options.AddPolicy(AppPolicies.ManageUsers, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ManageOrg, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ViewTeam, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Manager, RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ManageTeam, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Manager, RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ManageRoles, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.SuperAdmin));
    });

    options.AddPolicy(AppPolicies.IssueBonus, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Manager, RoleCodes.SuperAdmin));
    });

    options.AddPolicy(AppPolicies.AdjustBalances, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin));
    });

    options.AddPolicy(AppPolicies.ViewAllTransactions, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin));
    });

    options.AddPolicy(AppPolicies.ManageActivities, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ModerateActivities, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ManageSupport, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.Support));
    });

    options.AddPolicy(AppPolicies.ManageOrders, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.Merchandiser));
    });

    options.AddPolicy(AppPolicies.ManageCatalog, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.Merchandiser));
    });

    options.AddPolicy(AppPolicies.ManageNews, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources));
    });

    // Аналитика платформы и отделов: руководитель видит только свой отдел,
    // остальные роли с этой политикой видят все отделы.
    options.AddPolicy(AppPolicies.ViewAnalytics, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Manager,
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ViewRewardAnalytics, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources,
            RoleCodes.Merchandiser));
    });

    // Персональная аналитика сотрудников: только роли, которым разрешено видеть людей.
    options.AddPolicy(AppPolicies.ViewEmployeeAnalytics, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.ViewCollaborationAnalytics, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources));
    });

    // Модуль ИИ. Генерация черновиков доступна ролям, которые создают активности
    // и товары; рекомендации по людям — только ролям, работающим с персоналом.
    options.AddPolicy(AppPolicies.UseAiContent, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources,
            RoleCodes.Merchandiser));
    });

    options.AddPolicy(AppPolicies.UseAiEngagement, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.HumanResources));
    });

    options.AddPolicy(AppPolicies.UseAiMerchandising, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new ActiveUserRequirement());
        policy.AddRequirements(new AnyRoleRequirement(
            RoleCodes.Admin,
            RoleCodes.SuperAdmin,
            RoleCodes.Merchandiser));
    });
});

var app = builder.Build();

var deepSeekOptions = app.Services.GetRequiredService<IOptions<DeepSeekOptions>>().Value;

if (!deepSeekOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "DeepSeek API key is not configured. AI endpoints will return 503. " +
        "Set DeepSeek:ApiKey in user secrets or the DeepSeek__ApiKey environment variable.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseRequestTimingDiagnostics();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();
