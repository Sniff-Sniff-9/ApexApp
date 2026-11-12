using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Взаимодействие отделов: благодарности между сотрудниками разных отделов.
/// </summary>
/// <remarks>
/// В системе нет других сущностей совместной работы, поэтому показатели ограничены
/// благодарностями: отправка признания сотруднику другого отдела.
/// </remarks>
[ApiController]
[Route("api/analytics/collaboration")]
[Authorize(Policy = AppPolicies.ViewCollaborationAnalytics)]
public sealed class CollaborationAnalyticsController(
    ICollaborationAnalyticsService collaborationService) : ApiControllerBase
{
    /// <summary>
    /// Матрица благодарностей между отделами и доля межотдельских благодарностей.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат взаимодействия.</returns>
    /// <response code="200">Результат рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("thanks")]
    [ProducesResponseType(typeof(CollaborationThanksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CollaborationThanksDto>> GetThanks(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await collaborationService.GetThanksAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(CollaborationThanksDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }
}
