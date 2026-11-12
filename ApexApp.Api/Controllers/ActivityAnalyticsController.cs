using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Аналитика активностей: рейтинг активностей и детальная воронка одной активности.
/// </summary>
[ApiController]
[Route("api/analytics/activities")]
[Authorize(Policy = AppPolicies.ViewAnalytics)]
public sealed class ActivityAnalyticsController(
    IActivityAnalyticsService activityService,
    IAnalyticsScopeResolver scopeResolver) : ApiControllerBase
{
    /// <summary>
    /// Список активностей с показателями участия и завершения.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="format">Формат: <c>event</c>, <c>quest</c>, <c>survey</c> или пусто для всех.</param>
    /// <param name="sortBy">Сортировка: <c>participants</c>, <c>completionRate</c>, <c>rewardPoints</c>, <c>startDate</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список активностей.</returns>
    /// <response code="200">Список рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ActivityListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ActivityListDto>> GetActivities(
        [FromQuery] AnalyticsQueryRequest request,
        [FromQuery] string? format,
        [FromQuery] string? sortBy,
        CancellationToken cancellationToken)
    {
        var parsedSort = ParseSortBy(sortBy);

        if (parsedSort is null)
        {
            return BadRequest(new
            {
                message = "SortBy must be one of: participants, completionRate, rewardPoints, startDate."
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

        var result = await activityService.GetActivitiesAsync(
            query,
            format,
            parsedSort.Value,
            cancellationToken);

        return result.IsSuccess
            ? Ok(ActivityListDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Детальная аналитика активности: воронка, шаги задания и ряд активности.
    /// </summary>
    /// <param name="format">Формат: <c>event</c>, <c>quest</c> или <c>survey</c>.</param>
    /// <param name="id">Идентификатор активности.</param>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Детали активности.</returns>
    /// <response code="200">Детали рассчитаны.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="404">Активность не найдена.</response>
    [HttpGet("{format}/{id:long}")]
    [ProducesResponseType(typeof(ActivityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDetailDto>> GetActivityDetail(
        string format,
        long id,
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

        var result = await activityService.GetDetailAsync(query, format, id, cancellationToken);

        return result.IsSuccess
            ? Ok(ActivityDetailDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    private static ActivitySortBy? ParseSortBy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ActivitySortBy.Participants;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "participants" => ActivitySortBy.Participants,
            "completionrate" => ActivitySortBy.CompletionRate,
            "rewardpoints" => ActivitySortBy.RewardPoints,
            "startdate" => ActivitySortBy.StartDate,
            _ => null
        };
    }
}
