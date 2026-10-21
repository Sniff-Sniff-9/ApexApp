using ApexApp.Api.DTOs;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

[ApiController]
[Route("api/achievements")]
public sealed class AchievementsController(
    IAchievementService achievementService) : ApiControllerBase
{
    [HttpGet]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAll(
    CancellationToken cancellationToken)
    {
        var achievements = await achievementService.GetAllAsync(cancellationToken);

        return Ok(achievements.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AchievementDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var achievement = await achievementService.GetByIdAsync(
            id, cancellationToken);

        if (achievement is null)
            return NotFound();

        return Ok(ToDto(achievement));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAchievementRequest achievement,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(achievement.Code) ||
            string.IsNullOrWhiteSpace(achievement.Name))
        {
            return BadRequest("Code, Name are required.");
        }

        if (achievement.RewardAmount < 0)
            return BadRequest("RewardAmount cannot be negative.");

        try
        {
            var updated = await achievementService.UpdateAsync(
                id,
                achievement.Code,
                achievement.Name,
                achievement.Description,
                achievement.RewardAmount,
                achievement.IsActive,
                cancellationToken);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Conflict(
                "Could not update achievement. Its code may already exist.");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await achievementService.DeleteAsync(
                id, cancellationToken);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<UserAchievementDto>>>
    GetUserAchievements(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var achievements =
            await achievementService.GetUserAchievementsAsync(
                userId, cancellationToken);

        return Ok(achievements.Select(ToDto).ToList());
    }

    private static AchievementDto ToDto(Achievement a) =>
    new(
        a.Id,
        a.Code,
        a.Name,
        a.Description,
        a.IconUrl,
        a.RewardAmount,
        a.ConditionType,
        a.ConditionValue,
        a.IsActive,
        a.CreatedAt);

    private static UserAchievementDto ToDto(UserAchievement ua) =>
        new(
            ua.Id,
            ua.UserId,
            ua.AchievementId,
            ua.EarnedAt,
            ToDto(ua.Achievement));
}