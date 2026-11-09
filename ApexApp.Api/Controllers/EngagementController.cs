using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер персональных показателей текущего пользователя.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class EngagementController(IEngagementService engagementService) : ApiControllerBase
{
    /// <summary>
    /// Получить статус вовлечённости текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Рассчитывает уровень вовлечённости (Искра, Мерцание, Сияние, Сверхсияние, Зенит)
    /// по действиям пользователя за последние 30 дней. Источники активности:
    /// посещённые мероприятия, завершённые задания, подтверждённые шаги заданий,
    /// пройденные опросы, полученные и отправленные благодарности. Баллы взвешиваются
    /// по свежести действия: 0–7 дней — 100%, 8–14 дней — 70%, 15–30 дней — 40%.
    /// Ответ кешируется на 30 секунд и сбрасывается после действий пользователя.
    /// </remarks>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Рассчитанный статус вовлечённости.</returns>
    /// <response code="200">Статус успешно рассчитан.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав или учётная запись неактивна.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("engagement")]
    [ProducesResponseType(typeof(EngagementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EngagementDto>> GetEngagement(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await engagementService.GetCurrentAsync(userId.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(EngagementDto.From(result.Value!));
    }
}
