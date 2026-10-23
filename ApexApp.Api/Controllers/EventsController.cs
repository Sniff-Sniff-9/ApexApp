using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Пользовательские сценарии мероприятий: список, детали, регистрация,
/// отмена регистрации и награда за посещение, а также администрирование мероприятий
/// и подтверждение посещаемости.
/// </summary>
/// <param name="eventService">Сервис мероприятий.</param>
[ApiController]
[Route("api/events")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class EventsController(IEventService eventService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Получить страницу активных мероприятий, которые ещё не закончились.
    /// </summary>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список мероприятий успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<EventListItemDto>>> GetAll(
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

        var result = await eventService.GetAvailableAsync(
            userId.Value,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<EventListItemDto>(
            result.Items.Select(ToListItemDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Получить детали мероприятия вместе с регистрацией текущего пользователя.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Детали мероприятия успешно получены.</response>
    /// <response code="400">Некорректный идентификатор мероприятия.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено.</response>
    /// <response code="409">Мероприятие неактивно или уже закончилось, а регистрации нет.</response>
    [HttpGet("{eventId:long}")]
    [ProducesResponseType(typeof(EventDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDetailsDto>> GetById(
        long eventId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await eventService.GetDetailsAsync(eventId, userId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDetailsDto(result.Value!));
    }

    /// <summary>
    /// Зарегистрироваться на мероприятие.
    /// </summary>
    /// <remarks>
    /// Повторный запрос возвращает действующую регистрацию без создания дубликата.
    /// Отменённая регистрация восстанавливается, если есть свободные места.
    /// </remarks>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Действующая регистрация уже существовала.</response>
    /// <response code="201">Регистрация создана или восстановлена.</response>
    /// <response code="400">Некорректный идентификатор мероприятия.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено.</response>
    /// <response code="409">Мероприятие неактивно, закончилось или нет свободных мест.</response>
    [HttpPost("{eventId:long}/register")]
    [ProducesResponseType(typeof(EventRegistrationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EventRegistrationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventRegistrationResultDto>> Register(
        long eventId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await eventService.RegisterAsync(eventId, userId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var registration = result.Value!;
        var dto = new EventRegistrationResultDto(
            registration.Registration.EventId,
            registration.Registration.Id,
            registration.Registration.Status,
            registration.Registration.RegisteredAt,
            registration.AlreadyRegistered);

        return registration.AlreadyRegistered
            ? Ok(dto)
            : StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>
    /// Отменить свою регистрацию на мероприятие.
    /// </summary>
    /// <remarks>
    /// Отмена возможна, пока посещение не подтверждено. Повторный запрос идемпотентен.
    /// </remarks>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Регистрация отменена или уже была отменена.</response>
    /// <response code="400">Некорректный идентификатор мероприятия.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено или регистрация отсутствует.</response>
    /// <response code="409">Посещение уже подтверждено, поэтому отмена невозможна.</response>
    [HttpDelete("{eventId:long}/registration")]
    [ProducesResponseType(typeof(EventCancellationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventCancellationResultDto>> CancelRegistration(
        long eventId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await eventService.CancelRegistrationAsync(
            eventId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var cancellation = result.Value!;

        return Ok(new EventCancellationResultDto(
            cancellation.Registration.EventId,
            cancellation.Registration.Id,
            cancellation.Registration.Status,
            cancellation.AlreadyCancelled));
    }

    /// <summary>
    /// Получить награду за посещённое мероприятие.
    /// </summary>
    /// <remarks>
    /// Награда начисляется только после подтверждения посещения администратором.
    /// Повторный запрос не начисляет награду повторно.
    /// </remarks>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Награда начислена или уже была выдана ранее.</response>
    /// <response code="400">Некорректный идентификатор мероприятия.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено или регистрация отсутствует.</response>
    /// <response code="409">Посещение не подтверждено или награда не настроена.</response>
    [HttpPost("{eventId:long}/reward")]
    [ProducesResponseType(typeof(EventRewardResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventRewardResultDto>> ClaimReward(
        long eventId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await eventService.ClaimRewardAsync(eventId, userId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(new EventRewardResultDto(
            eventId,
            result.Value!.Amount,
            result.Value!.BalanceAfter,
            result.Value!.AlreadyGranted));
    }

    /// <summary>
    /// Получить страницу мероприятий для административного просмотра.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех мероприятий.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список мероприятий успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("admin")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(PagedResult<AdminEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminEventDto>>> GetForAdministration(
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        var result = await eventService.GetForAdministrationAsync(
            isActive,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<AdminEventDto>(
            result.Items.Select(ToAdminDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Создать мероприятие.
    /// </summary>
    /// <param name="request">Данные мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Мероприятие успешно создано.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminEventDto>> Create(
        [FromBody] CreateEventRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await eventService.CreateAsync(
            new CreateEventCommand(
                request.Title,
                request.Description,
                request.StartAt,
                request.EndAt,
                request.Location,
                request.MaxParticipants,
                request.RewardAmount,
                request.IsActive),
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var dto = ToAdminDto(result.Value!);

        return CreatedAtAction(nameof(GetById), new { eventId = dto.Id }, dto);
    }

    /// <summary>
    /// Обновить мероприятие.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="request">Данные мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Мероприятие успешно обновлено.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено.</response>
    [HttpPut("{eventId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        long eventId,
        [FromBody] UpdateEventRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await eventService.UpdateAsync(
            eventId,
            new UpdateEventCommand(
                request.Title,
                request.Description,
                request.StartAt,
                request.EndAt,
                request.Location,
                request.MaxParticipants,
                request.RewardAmount,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Деактивировать мероприятие.
    /// </summary>
    /// <remarks>
    /// Мероприятие не удаляется физически: регистрации и выданные награды сохраняются.
    /// </remarks>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Мероприятие деактивировано.</response>
    /// <response code="400">Некорректный идентификатор мероприятия.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено.</response>
    [HttpDelete("{eventId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Deactivate(
        long eventId,
        CancellationToken cancellationToken)
    {
        var result = await eventService.DeactivateAsync(eventId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Получить страницу участников мероприятия.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список участников успешно получен.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие не найдено.</response>
    [HttpGet("{eventId:long}/participants")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(PagedResult<EventParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<EventParticipantDto>>> GetParticipants(
        long eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        var result = await eventService.GetParticipantsAsync(
            eventId,
            page,
            pageSize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var participants = result.Value!;

        return Ok(new PagedResult<EventParticipantDto>(
            participants.Items.Select(ToDto).ToList(),
            participants.TotalCount,
            participants.Page,
            participants.PageSize));
    }

    /// <summary>
    /// Подтвердить посещение или отметить неявку участника.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="participantId">Идентификатор регистрации.</param>
    /// <param name="request">Новый статус участника.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Статус участника обновлён.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Мероприятие или участник не найдены.</response>
    /// <response code="409">Статус нельзя изменить: награда уже выдана или регистрация отменена.</response>
    [HttpPut("{eventId:long}/participants/{participantId:long}/attendance")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(EventAttendanceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventAttendanceResultDto>> SetAttendance(
        long eventId,
        long participantId,
        [FromBody] SetEventAttendanceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await eventService.SetAttendanceAsync(
            eventId,
            participantId,
            new EventAttendanceCommand(request.Status),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var participant = result.Value!;

        return Ok(new EventAttendanceResultDto(
            participant.Participant.EventId,
            participant.Participant.Id,
            participant.Participant.Status,
            participant.Participant.AttendedAt,
            participant.AlreadyInStatus));
    }

    private static EventListItemDto ToListItemDto(EventSummary summary) =>
        new(
            summary.Event.Id,
            summary.Event.Title,
            summary.Event.Description,
            summary.Event.StartAt,
            summary.Event.EndAt,
            summary.Event.Location,
            summary.Event.MaxParticipants,
            summary.RegisteredCount,
            summary.Event.RewardAmount,
            summary.Registration?.Status);

    private static EventDetailsDto ToDetailsDto(EventDetailsResult details) =>
        new(
            details.Event.Id,
            details.Event.Title,
            details.Event.Description,
            details.Event.StartAt,
            details.Event.EndAt,
            details.Event.Location,
            details.Event.MaxParticipants,
            details.RegisteredCount,
            details.Event.RewardAmount,
            details.Event.IsActive,
            details.IsOpen,
            details.Registration is null ? null : ToDto(details.Registration));

    private static EventParticipantDto ToDto(EventParticipant participant) =>
        new(
            participant.Id,
            participant.UserId,
            participant.User?.FirstName ?? string.Empty,
            participant.User?.LastName ?? string.Empty,
            participant.User?.AvatarUrl,
            participant.Status,
            participant.RegisteredAt,
            participant.AttendedAt,
            participant.RewardClaimed);

    private static AdminEventDto ToAdminDto(EventSummary summary) =>
        new(
            summary.Event.Id,
            summary.Event.Title,
            summary.Event.Description,
            summary.Event.StartAt,
            summary.Event.EndAt,
            summary.Event.Location,
            summary.Event.MaxParticipants,
            summary.RegisteredCount,
            summary.Event.RewardAmount,
            summary.Event.IsActive,
            summary.Event.CreatedBy,
            summary.Event.CreatedAt,
            summary.Event.UpdatedAt);

    private static AdminEventDto ToAdminDto(Event eventItem) =>
        new(
            eventItem.Id,
            eventItem.Title,
            eventItem.Description,
            eventItem.StartAt,
            eventItem.EndAt,
            eventItem.Location,
            eventItem.MaxParticipants,
            0,
            eventItem.RewardAmount,
            eventItem.IsActive,
            eventItem.CreatedBy,
            eventItem.CreatedAt,
            eventItem.UpdatedAt);

    private static string? ValidatePaging(int page, int pageSize) =>
        page < 1
            ? "Page must be greater than or equal to 1."
            : pageSize is < 1 or > MaxPageSize
                ? $"PageSize must be between 1 and {MaxPageSize}."
                : null;
}
