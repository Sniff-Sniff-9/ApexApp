using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Аналитика вовлечённости: исторический тренд и сотрудники со снижением активности.
/// </summary>
[ApiController]
[Route("api/analytics/engagement")]
[Authorize(Policy = AppPolicies.ViewAnalytics)]
public sealed class EngagementAnalyticsController(
    IEngagementAnalyticsService engagementService,
    IAnalyticsScopeResolver scopeResolver) : ApiControllerBase
{
    /// <summary>
    /// Тренд вовлечённости по периодам. Расчёт повторно использует действующую формулу
    /// вовлечённости, применённую к данным на конец каждого периода.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Тренд вовлечённости.</returns>
    /// <response code="200">Тренд рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("trend")]
    [ProducesResponseType(typeof(EngagementTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EngagementTrendDto>> GetTrend(
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

        var result = await engagementService.GetTrendAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(EngagementTrendDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Сотрудники со снижающейся вовлечённостью. Доступно только ролям, которым разрешена
    /// персональная аналитика; идентификатор сотрудника обезличен, имена передаются отдельно.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список сотрудников.</returns>
    /// <response code="200">Список рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("declining")]
    [Authorize(Policy = AppPolicies.ViewEmployeeAnalytics)]
    [ProducesResponseType(typeof(DecliningEmployeesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DecliningEmployeesDto>> GetDeclining(
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

        // Политика уже гарантирует право видеть сотрудников, поэтому имена раскрываются.
        var result = await engagementService.GetDecliningAsync(query, includeNames: true, cancellationToken);

        return result.IsSuccess
            ? Ok(DecliningEmployeesDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }
}
