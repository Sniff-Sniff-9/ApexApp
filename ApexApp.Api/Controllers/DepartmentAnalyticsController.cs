using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Сравнение отделов и детализация одного отдела.
/// </summary>
[ApiController]
[Route("api/analytics/departments")]
[Authorize(Policy = AppPolicies.ViewAnalytics)]
public sealed class DepartmentAnalyticsController(
    IDepartmentAnalyticsService departmentService,
    IAnalyticsScopeResolver scopeResolver) : ApiControllerBase
{
    /// <summary>
    /// Сравнение отделов по нормализованным показателям активности, признания и вознаграждений.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сравнение отделов.</returns>
    /// <response code="200">Сравнение рассчитано.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet]
    [ProducesResponseType(typeof(DepartmentAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DepartmentAnalyticsDto>> GetDepartments(
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

        var result = await departmentService.GetAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(DepartmentAnalyticsDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Детализация отдела: показатели, ряд участников и форматы активности.
    /// </summary>
    /// <param name="id">Идентификатор отдела.</param>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Детализация отдела.</returns>
    /// <response code="200">Детализация рассчитана.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="403">Отдел недоступен вызывающему коду.</response>
    /// <response code="404">Отдел не найден.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDetailDto>> GetDepartment(
        int id,
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(User, id, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AnalyticsQueryBinder.TryCreate(request, scope.DepartmentId, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await departmentService.GetDetailAsync(query, id, cancellationToken);

        return result.IsSuccess
            ? Ok(DepartmentDetailDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }
}
