using ApexApp.Api.Authorization;
using ApexApp.Api.Caching;
using ApexApp.Api.Diagnostics;
using ApexApp.Api.Swagger;
using ApexApp.Infrastructure.Auth;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Локальная диагностика производительности: включается только в Development
// и только при Diagnostics:Timing:Enabled=true (по умолчанию false).
var requestTimingEnabled = builder.Environment.IsRequestTimingEnabled(builder.Configuration);
var includeSqlInTimings = requestTimingEnabled &&
    builder.Configuration.GetValue<bool>($"{DiagnosticsOptions.SectionName}:IncludeSql");

builder.Services.AddDbContext<AppDbContext>(options =>
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
        });

    if (requestTimingEnabled)
    {
        options.AddInterceptors(new SqlTimingInterceptor(includeSqlInTimings));
    }
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
builder.Services.AddScoped<ActivityRewardGranter>();
builder.Services.AddScoped<IActivityRewardService, ActivityRewardService>();
builder.Services.AddScoped<IQuestService, QuestService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ISurveyService, SurveyService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();

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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{supabaseUrl}/auth/v1";
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
            RoleClaimType = "user_role"
        };
    });

// Supabase — внешний HTTP-сервис: ограниченный таймаут не должен превращать
// недоступность провайдера в запрос, висящий 100 секунд (значение по умолчанию).
builder.Services.AddHttpClient(SupabaseHttpClient.Name, client =>
{
    client.Timeout = SupabaseHttpClient.Timeout;
});

// Настройка политик авторизации: проверка аутентификации пользователя,
// его активности и наличия ролей для доступа к функционалу приложения.
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
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin, RoleCodes.Manager));
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
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin));
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
});

var app = builder.Build();

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

app.MapControllers();

app.Run();
