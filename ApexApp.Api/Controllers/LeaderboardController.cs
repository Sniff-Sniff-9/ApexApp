using ApexApp.Api.Authorization;
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

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 365)
        {
            return BadRequest(new
            {
                message = "Days must be between 1 and 365."
            });
        }

        if (limit is < 1 or > 100)
        {
            return BadRequest(new
            {
                message = "Limit must be between 1 and 100."
            });
        }

        var result = await _leaderboardService.GetAsync(
            days,
            limit,
            cancellationToken);

        return Ok(result);
    }
}