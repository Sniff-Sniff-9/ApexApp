using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;


/// <summary>
/// Контроллер, отвечающий за управление благодарностями пользователей.
/// </summary>
[ApiController]
[Route("api/thanks")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class ThanksController : ApiControllerBase
{
    private readonly IThankService _thankService;


    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ThanksController"/>.
    /// </summary>
    /// <param name="thankService">Сервис благодарностей.</param>
    public ThanksController(IThankService thankService)
    {
        _thankService = thankService;
    }

    /// <summary>
    /// Получить список благодарностей пользователя, отправленных другими пользователями.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о благодарностях текущего пользователя, которые были отправлены ЕМУ.
    /// </remarks>
    /// <returns>Список благодарностей пользователя.</returns>
    /// <response code="200">Благодарности успешно получены.</response>
    /// /// /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("for-me")]
    public async Task<IActionResult> GetAllForMe(
    CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var thanks = await _thankService.GetAllAsync(userId.Value, cancellationToken);

        return Ok(thanks.Select(t => new
        {
            t.Id,
            t.FromUserId,
            FromUserName = t.FromUser.Id,
            t.ToUserId,
            ToUserName = t.ToUser.Id,
            t.Amount,
            t.Message,
            t.IsPublic,
            t.CreatedAt
        }));
    }

    /// <summary>
    /// Получить список благодарностей пользователя для других пользователей.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о благодарностях текущего пользователя, которые были отправлены ОТ НЕГО.
    /// </remarks>
    /// <returns>Список благодарностей пользователя.</returns>
    /// <response code="200">Благодарности успешно получены.</response>
    /// /// /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("from-me")]
    public async Task<IActionResult> GetAllFromMe(
    CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var thanks = await _thankService.GetAllFromMeAsync(userId.Value, cancellationToken);

        return Ok(thanks.Select(t => new
        {
            t.Id,
            t.FromUserId,
            FromUserName = t.FromUser.Id,
            t.ToUserId,
            ToUserName = t.ToUser.Id,
            t.Amount,
            t.Message,
            t.IsPublic,
            t.CreatedAt
        }));
    }

    /// <summary>
    /// Получить полученную пользователем благодарность по идентификатору.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о благодарности, которая была отправлена пользователю, по идентификатору.
    /// </remarks>
    /// <returns>Информация о благодарности пользователя.</returns>
    /// <response code="200">Благодарности успешно получены.</response>
    /// <response code="404">Благодарность не найдена.</response>
    /// /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var thank = await _thankService.GetByIdAsync(userId.Value, id, cancellationToken);

        if (thank is null)
            return NotFound(new { message = "Thanks was not found." });

        return Ok(new
        {
            thank.Id,
            thank.FromUserId,
            FromUserName = thank.FromUser.Id,
            thank.ToUserId,
            ToUserName = thank.ToUser.Id,
            thank.Amount,
            thank.Message,
            thank.IsPublic,
            thank.CreatedAt
        });
    }

    /// <summary>
    /// Отправить благодарность пользователю.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о благодарности пользователя.
    /// </remarks>
    /// <returns>Информация о благодарности пользователя.</returns>
    /// <response code="201">Благoдарность успешно отправлена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateThankRequest request,
        CancellationToken cancellationToken)
    {
        var fromUserId = User.GetUserId();

        if (fromUserId is null)
            return Unauthorized();

        var result = await _thankService.CreateAsync(
            fromUserId.Value, request, cancellationToken);

        if (result.Error is not null)
            return StatusCode(
                result.StatusCode,
                new { message = result.Error });

        var thank = result.Data!;

        return StatusCode(StatusCodes.Status201Created, new
        {
            thank.Id,
            thank.FromUserId,
            thank.ToUserId,
            thank.Amount,
            thank.Message,
            thank.IsPublic,
            thank.CreatedAt
        });
    }

    /// <summary>
    /// Отправить поощрение пользователю от руководителя.
    /// </summary>
    /// <returns>Информация о поощрении пользователя.</returns>
    /// <response code="201">Поощрение успешно отправлено .</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpPost("manager-bonus")]
    [Authorize(Policy = AppPolicies.IssueBonus)]
    public async Task<IActionResult> GiveManagerBonus(
    [FromBody] CreateManagerBonusRequest request,
    CancellationToken cancellationToken)
    {
        var managerId = User.GetUserId();

        if (managerId is null)
            return Unauthorized();

        var result = await _thankService.GiveManagerBonusAsync(
            managerId.Value,
            request,
            cancellationToken);

        if (result.Error is not null)
        {
            return StatusCode(
                result.StatusCode,
                new { message = result.Error });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Manager bonus awarded successfully.",
            recipientId = request.ToUserId,
            request.Amount,
            request.Message
        });
    }
}