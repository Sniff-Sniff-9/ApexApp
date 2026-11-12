using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Обзор платформы, временные ряды, сравнение форматов, наблюдения и качество данных.
/// </summary>
/// <remarks>
/// Все показатели рассчитываются на сервере по существующим сущностям. Окна задаются
/// полуинтервалом [начало, конец) в UTC; момент расчёта общий для всех чисел ответа.
/// </remarks>
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = AppPolicies.ViewAnalytics)]
public sealed class AnalyticsController(
    IAnalyticsOverviewService overviewService,
    IAnalyticsSeriesService seriesService,
    IAnalyticsInsightService insightService,
    IAnalyticsHealthService healthService,
    IAnalyticsScopeResolver scopeResolver) : ApiControllerBase
{
    /// <summary>
    /// Ключевые показатели платформы, изменения к предыдущему окну и сравнение форматов.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обзор платформы.</returns>
    /// <response code="200">Обзор рассчитан.</response>
    /// <response code="400">Некорректные параметры окна.</response>
    /// <response code="403">Недостаточно прав для запрошенного отдела.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(PlatformOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PlatformOverviewDto>> GetOverview(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(User, request.DepartmentId, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AnalyticsQueryBinder.TryCreate(request, scope.DepartmentId, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await overviewService.GetAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(PlatformOverviewDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Временной ряд выбранного показателя с шагом день, неделя или месяц.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="metric">Показатель: <c>Participants</c>, <c>Actions</c>, <c>EventsAttended</c>,
    /// <c>QuestsCompleted</c>, <c>SurveysSubmitted</c>, <c>ThanksSent</c>, <c>PointsEarned</c>,
    /// <c>PointsSpent</c> или <c>Orders</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ряд показателя.</returns>
    /// <response code="200">Ряд рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("timeseries")]
    [ProducesResponseType(typeof(SeriesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SeriesDto>> GetTimeSeries(
        [FromQuery] AnalyticsQueryRequest request,
        [FromQuery] string? metric,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AnalyticsSeriesMetric>(metric, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return BadRequest(new
            {
                message = "Metric must be one of: Participants, Actions, EventsAttended, " +
                          "QuestsCompleted, SurveysSubmitted, ThanksSent, PointsEarned, PointsSpent, Orders."
            });
        }

        var scope = await scopeResolver.ResolveAsync(User, request.DepartmentId, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AnalyticsQueryBinder.TryCreate(request, scope.DepartmentId, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await seriesService.GetAsync(query, parsed, cancellationToken);

        return result.IsSuccess
            ? Ok(SeriesDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Сравнение форматов активности: охват, завершение, повторность и стоимость в баллах.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сравнение форматов.</returns>
    /// <response code="200">Сравнение рассчитано.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("activity-formats")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityFormatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ActivityFormatDto>>> GetActivityFormats(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(User, request.DepartmentId, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AnalyticsQueryBinder.TryCreate(request, scope.DepartmentId, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await overviewService.GetAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value!.Formats.Select(ActivityFormatDto.From).ToList())
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Наблюдения по данным: низкое завершение, падение вовлечённости, простой каталога и другие.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Набор наблюдений.</returns>
    /// <response code="200">Наблюдения рассчитаны.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("insights")]
    [ProducesResponseType(typeof(InsightReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InsightReportDto>> GetInsights(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(User, request.DepartmentId, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AnalyticsQueryBinder.TryCreate(request, scope.DepartmentId, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await insightService.GetAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(InsightReportDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Проверки качества данных, влияющих на корректность аналитики.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние данных.</returns>
    /// <response code="200">Проверки выполнены.</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(AnalyticsHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnalyticsHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await healthService.GetAsync(cancellationToken);

        return result.IsSuccess
            ? Ok(AnalyticsHealthDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }
}
