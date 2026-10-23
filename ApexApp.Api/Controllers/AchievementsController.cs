using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за управление достижениями и достижениями пользователей.
/// Чтение доступно активным пользователям, изменение и удаление — администраторам.
/// </summary>
/// <param name="achievementService">Сервис достижений.</param>
[ApiController]
[Route("api/achievements")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class AchievementsController(
    IAchievementService achievementService) : ApiControllerBase
{
    /// <summary>
    /// Получить список достижений.
    /// </summary>
    /// <remarks>
    /// Возвращает все достижения, включая неактивные, упорядоченные по идентификатору.
    /// </remarks>
    /// <returns>Список достижений.</returns>
    /// <response code="200">Список достижений успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AchievementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAll(
    CancellationToken cancellationToken)
    {
        var achievements = await achievementService.GetAllAsync(cancellationToken);

        return Ok(achievements.Select(ToDto).ToList());
    }

    /// <summary>
    /// Получить достижение по идентификатору.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о достижении с указанным идентификатором.
    /// </remarks>
    /// <returns>Информация о достижении.</returns>
    /// <response code="200">Достижение успешно получено.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Достижение не найдено.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AchievementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var achievement = await achievementService.GetByIdAsync(
            id, cancellationToken);

        if (achievement is null)
        {
            return NotFound(new { message = "Achievement was not found." });
        }

        return Ok(ToDto(achievement));
    }

    /// <summary>
    /// Обновить достижение.
    /// </summary>
    /// <remarks>
    /// Обновляет код, название, описание, размер вознаграждения и признак активности достижения.
    /// </remarks>
    /// <returns>Ответ без содержимого при успешном обновлении.</returns>
    /// <response code="204">Достижение успешно обновлено.</response>
    /// <response code="400">Некорректные данные запроса: код и название обязательны, вознаграждение не может быть отрицательным.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Достижение не найдено.</response>
    /// <response code="409">Конфликт: достижение с таким кодом уже существует.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAchievementRequest achievement,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(achievement.Code) ||
            string.IsNullOrWhiteSpace(achievement.Name))
        {
            return BadRequest(new { message = "Code and Name are required." });
        }

        if (achievement.RewardAmount < 0)
        {
            return BadRequest(new { message = "RewardAmount cannot be negative." });
        }

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
            {
                return NotFound(new { message = "Achievement was not found." });
            }

            return NoContent();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Conflict(new
            {
                message = "Could not update achievement. Its code may already exist."
            });
        }
    }

    /// <summary>
    /// Удалить достижение.
    /// </summary>
    /// <remarks>
    /// Удаляет достижение, если оно еще не было выдано ни одному пользователю.
    /// </remarks>
    /// <returns>Ответ без содержимого при успешном удалении.</returns>
    /// <response code="204">Достижение успешно удалено.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Достижение не найдено.</response>
    /// <response code="409">Конфликт: достижение уже выдано пользователям и не может быть удалено.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await achievementService.DeleteAsync(
                id, cancellationToken);

            if (!deleted)
            {
                return NotFound(new { message = "Achievement was not found." });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Получить достижения пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает достижения, полученные указанным пользователем, упорядоченные по дате получения (сначала новые).
    /// </remarks>
    /// <returns>Список достижений пользователя.</returns>
    /// <response code="200">Достижения пользователя успешно получены.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<UserAchievementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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