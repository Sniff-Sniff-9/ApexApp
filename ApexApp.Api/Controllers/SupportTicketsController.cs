using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Обращения в поддержку: создание обращений и переписка для сотрудников,
/// а также управление обращениями для сотрудников поддержки.
/// </summary>
[ApiController]
[Route("api/support/tickets")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class SupportTicketsController(
    ISupportTicketService supportTicketService,
    IAuthorizationService authorizationService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private const string InvalidStatusMessage =
        "Invalid ticket status. Allowed values: Open, InProgress, Resolved, Closed.";

    /// <summary>
    /// Создать обращение в поддержку.
    /// </summary>
    /// <remarks>
    /// Автор обращения определяется по данным токена доступа. Обращение создаётся
    /// вместе с первым сообщением в одной транзакции.
    /// </remarks>
    /// <param name="request">Тема обращения и текст первого сообщения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданное обращение вместе с историей сообщений.</returns>
    /// <response code="201">Обращение успешно создано.</response>
    /// <response code="400">Некорректные данные обращения.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SupportTicketDetails), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SupportTicketDetails>> Create(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await supportTicketService.CreateAsync(
            userId.Value,
            new CreateSupportTicketCommand(request.Subject, request.Message),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Получить страницу собственных обращений в поддержку.
    /// </summary>
    /// <remarks>
    /// Возвращаются только обращения текущего пользователя, начиная с самых новых.
    /// </remarks>
    /// <param name="status">Необязательный фильтр по статусу обращения.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница обращений текущего пользователя.</returns>
    /// <response code="200">Список обращений успешно получен.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupportTicketSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<SupportTicketSummary>>> GetMine(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        if (!TryParseStatus(status, out var ticketStatus))
        {
            return BadRequest(new { message = InvalidStatusMessage });
        }

        var result = await supportTicketService.GetMyAsync(
            userId.Value,
            ticketStatus,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Получить обращение вместе с историей сообщений.
    /// </summary>
    /// <remarks>
    /// Пользователь может получить только собственное обращение. Пользователи
    /// с правом управления поддержкой могут получить любое обращение.
    /// </remarks>
    /// <param name="id">Идентификатор обращения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обращение вместе с историей сообщений.</returns>
    /// <response code="200">Обращение успешно получено.</response>
    /// <response code="400">Некорректный идентификатор обращения.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Обращение не найдено.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SupportTicketDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportTicketDetails>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var canManage = await CanManageSupportAsync();

        var result = await supportTicketService.GetByIdAsync(
            userId.Value,
            canManage,
            id,
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Отправить сообщение в обращение.
    /// </summary>
    /// <remarks>
    /// Автор сообщения определяется по данным токена доступа. Пользователь может
    /// отвечать только в собственных обращениях, сотрудник поддержки — в любых.
    /// </remarks>
    /// <param name="id">Идентификатор обращения.</param>
    /// <param name="request">Текст сообщения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданное сообщение.</returns>
    /// <response code="201">Сообщение успешно отправлено.</response>
    /// <response code="400">Некорректные данные сообщения.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Обращение не найдено.</response>
    [HttpPost("{id:long}/messages")]
    [ProducesResponseType(typeof(SupportMessageItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportMessageItem>> AddMessage(
        long id,
        [FromBody] SendSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var canManage = await CanManageSupportAsync();

        var result = await supportTicketService.AddMessageAsync(
            userId.Value,
            canManage,
            id,
            new SendSupportMessageCommand(request.Message),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Получить страницу всех обращений в поддержку.
    /// </summary>
    /// <remarks>
    /// Доступно только сотрудникам поддержки. Фильтры <c>status</c> и <c>assignedTo</c>
    /// необязательны; обращения упорядочены по дате последнего изменения.
    /// </remarks>
    /// <param name="status">Необязательный фильтр по статусу обращения.</param>
    /// <param name="assignedTo">Необязательный фильтр по назначенному сотруднику.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница всех обращений.</returns>
    /// <response code="200">Список обращений успешно получен.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав для управления поддержкой.</response>
    [HttpGet("all")]
    [Authorize(Policy = AppPolicies.ManageSupport)]
    [ProducesResponseType(typeof(PagedResult<SupportTicketSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SupportTicketSummary>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        if (assignedTo == Guid.Empty)
        {
            return BadRequest(new { message = "A valid assignee ID is required." });
        }

        if (!TryParseStatus(status, out var ticketStatus))
        {
            return BadRequest(new { message = InvalidStatusMessage });
        }

        var result = await supportTicketService.GetAllAsync(
            ticketStatus,
            assignedTo,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Изменить статус обращения.
    /// </summary>
    /// <remarks>
    /// Доступно только сотрудникам поддержки. Допустимые статусы:
    /// <c>Open</c>, <c>InProgress</c>, <c>Resolved</c>, <c>Closed</c>.
    /// </remarks>
    /// <param name="id">Идентификатор обращения.</param>
    /// <param name="request">Новый статус обращения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обращение с обновлённым статусом.</returns>
    /// <response code="200">Статус обращения обновлён.</response>
    /// <response code="400">Некорректный статус обращения.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав для управления поддержкой.</response>
    /// <response code="404">Обращение не найдено.</response>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = AppPolicies.ManageSupport)]
    [ProducesResponseType(typeof(SupportTicketDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportTicketDetails>> UpdateStatus(
        long id,
        [FromBody] UpdateSupportTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(request.Status, out var ticketStatus) || ticketStatus is null)
        {
            return BadRequest(new { message = InvalidStatusMessage });
        }

        var result = await supportTicketService.UpdateStatusAsync(
            id,
            ticketStatus.Value,
            cancellationToken);

        return ToActionResult(result);
    }


    /// <summary>
    /// Назначить обращение сотруднику поддержки или снять назначение.
    /// </summary>
    /// <remarks>
    /// Доступно только сотрудникам поддержки. Чтобы снять назначение,
    /// передайте <c>null</c> в поле <c>userId</c>.
    /// </remarks>
    /// <param name="id">Идентификатор обращения.</param>
    /// <param name="request">Идентификатор сотрудника поддержки или <c>null</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Обращение с обновлённым назначением.</returns>
    /// <response code="200">Назначение обращения обновлено.</response>
    /// <response code="400">Некорректный сотрудник поддержки.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав для управления поддержкой.</response>
    /// <response code="404">Обращение или сотрудник не найдены.</response>
    [HttpPatch("{id:long}/assigned-to")]
    [Authorize(Policy = AppPolicies.ManageSupport)]
    [ProducesResponseType(typeof(SupportTicketDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportTicketDetails>> Assign(
        long id,
        [FromBody] AssignSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supportTicketService.AssignAsync(
            id,
            request.UserId,
            cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Проверяет, может ли текущий пользователь управлять обращениями в поддержку.
    /// </summary>
    private async Task<bool> CanManageSupportAsync()
    {
        var result = await authorizationService.AuthorizeAsync(User, AppPolicies.ManageSupport);

        return result.Succeeded;
    }

    /// <summary>
    /// Проверяет параметры постраничной выдачи.
    /// </summary>
    /// <param name="page">Номер страницы.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <returns>Сообщение об ошибке или <c>null</c>, если параметры корректны.</returns>
    private static string? ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            return "Page must be greater than or equal to 1.";
        }

        return pageSize is < 1 or > MaxPageSize
            ? $"Page size must be between 1 and {MaxPageSize}."
            : null;
    }

    /// <summary>
    /// Разбирает необязательный статус обращения из строки запроса или тела запроса.
    /// </summary>
    /// <param name="value">Текстовое значение статуса.</param>
    /// <param name="status">Разобранный статус или <c>null</c>, если значение не задано.</param>
    /// <returns><c>true</c>, если значение корректно или отсутствует.</returns>
    private static bool TryParseStatus(string? value, out TicketStatus? status)
    {
        status = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value
            .Trim()
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        if (!Enum.TryParse<TicketStatus>(normalized, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return false;
        }

        status = parsed;

        return true;
    }
}

