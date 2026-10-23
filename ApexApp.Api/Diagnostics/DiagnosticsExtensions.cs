namespace ApexApp.Api.Diagnostics;

/// <summary>
/// Регистрация локальной диагностики производительности.
/// Всё активируется только при <c>IsDevelopment()</c> и <c>Diagnostics:Timing:Enabled=true</c>.
/// </summary>
internal static class DiagnosticsExtensions
{
    /// <summary>Возвращает <c>true</c>, если сбор таймингов EF Core должен быть подключён.</summary>
    public static bool IsRequestTimingEnabled(this IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() &&
        configuration.GetValue<bool>($"{DiagnosticsOptions.SectionName}:Enabled");

    /// <summary>Привязывает настройки диагностики к секции конфигурации (без побочных эффектов).</summary>
    public static IServiceCollection AddRequestTimingDiagnostics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));
        return services;
    }

    /// <summary>Подключает middleware таймингов, если диагностика включена в Development.</summary>
    public static WebApplication UseRequestTimingDiagnostics(this WebApplication app)
    {
        var settings = app.Configuration
            .GetSection(DiagnosticsOptions.SectionName)
            .Get<DiagnosticsOptions>() ?? new DiagnosticsOptions();

        if (!app.Environment.IsDevelopment() || !settings.Enabled)
        {
            return app;
        }

        app.UseMiddleware<RequestTimingMiddleware>();

        app.Logger.LogInformation(
            "PERF diagnostics enabled: IncludeSql={IncludeSql}, LogAllRequests={LogAllRequests}, SlowRequestMs={SlowRequestMs}",
            settings.IncludeSql,
            settings.LogAllRequests,
            settings.SlowRequestMs);

        return app;
    }
}
