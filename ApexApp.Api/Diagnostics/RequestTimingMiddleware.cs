using System.Globalization;
using Microsoft.Extensions.Options;

namespace ApexApp.Api.Diagnostics;

/// <summary>
/// Измеряет полное время запроса (TTFB/общую длительность) и пишет одну строку лога с разбивкой по этапам.
/// Заголовки добавляются только в Development и только при включённой диагностике.
/// </summary>
internal sealed class RequestTimingMiddleware(
    RequestDelegate next,
    IOptions<DiagnosticsOptions> options,
    ILogger<RequestTimingMiddleware> logger)
{
    internal const string DurationHeaderName = "X-Request-Duration-Ms";
    internal const string ServerTimingHeaderName = "Server-Timing";

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        var timing = RequestTimingContext.Begin();

        try
        {
            context.Response.OnStarting(() =>
            {
                var elapsedMs = timing.TotalMs;
                context.Response.Headers[DurationHeaderName] =
                    elapsedMs.ToString("F1", CultureInfo.InvariantCulture);
                context.Response.Headers[ServerTimingHeaderName] = timing.ToServerTiming(elapsedMs);
                return Task.CompletedTask;
            });

            await next(context);
        }
        finally
        {
            var totalMs = timing.TotalMs;

            if (settings.LogAllRequests || totalMs >= settings.SlowRequestMs)
            {
                logger.LogInformation(
                    "PERF {Method} {Path} -> {StatusCode} total={TotalMs}ms sql={SqlCount}/{SqlTotalMs}ms stages: {Stages}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    totalMs.ToString("F1", CultureInfo.InvariantCulture),
                    timing.SqlCount,
                    timing.SqlTotalMs.ToString("F1", CultureInfo.InvariantCulture),
                    timing.DescribeStages());
            }

            RequestTimingContext.End();
        }
    }
}
