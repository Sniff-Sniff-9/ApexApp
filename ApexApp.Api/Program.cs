using ApexApp.Api.Authorization;
using ApexApp.Api.Swagger;
using ApexApp.Infrastructure.Auth;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.MapEnum<BalanceType>("balance_type");
            npgsql.MapEnum<TransactionType>("transaction_type");
            npgsql.MapEnum<OrderStatus>("order_status");
            npgsql.MapEnum<TicketStatus>("ticket_status");
            npgsql.MapEnum<QuestStepCompletionType>("quest_step_completion_type");
        }));

builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IBalanceRepository, BalanceRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBalanceService, BalanceService>();
builder.Services.AddScoped<IClaimsTransformation, UserClaimsTransformation>();
builder.Services.AddSingleton<IAuthorizationHandler, ActiveUserHandler>();
builder.Services.AddScoped<IThankService, ThankService>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyRoleHandler>();

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
        policy.AddRequirements(new AnyRoleRequirement(RoleCodes.Admin, RoleCodes.SuperAdmin));
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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
