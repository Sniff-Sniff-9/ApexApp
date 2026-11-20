using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class LeaderboardController: ApiControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="LeaderboardController"/>.
    /// </summary>
    /// <param name="leaderboardService">Сервис лидерборда.</param>
    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    /// <summary>
    /// Возвращает лидерборд по баллу вовлечённости. Окно расчёта фиксировано
    /// и совпадает с окном статуса вовлечённости сотрудника.
    /// </summary>
    /// <param name="query">Параметры лидерборда.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] LeaderboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > 100)
        {
            return BadRequest(new
            {
                message = "Limit must be between 1 and 100."
            });
        }

        var result = await _leaderboardService.GetAsync(
            query.Limit,
            cancellationToken);

        return Ok(result);
    }
}
