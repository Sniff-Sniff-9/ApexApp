using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Пользовательские сценарии квестов: список доступных квестов, детали, участие,
/// выполнение шагов и завершение квеста с начислением награды.
/// </summary>
[ApiController]
[Route("api/quests")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class QuestsController(IQuestService questService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Получить страницу доступных квестов вместе со статусом участия текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращаются только активные квесты, находящиеся в пределах дат начала и окончания.
    /// </remarks>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница доступных квестов.</returns>
    /// <response code="200">Список квестов успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<QuestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<QuestListItemDto>>> GetAll(
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

        var result = await questService.GetAvailableAsync(
            userId.Value,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<QuestListItemDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Получить страницу квестов, в которых участвует текущий пользователь.
    /// </summary>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница участий в квестах с прогрессом выполнения шагов.</returns>
    /// <response code="200">Список участий успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<MyQuestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<MyQuestDto>>> GetMy(
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

        var result = await questService.GetMyAsync(
            userId.Value,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<MyQuestDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Получить детали квеста вместе с шагами и участием текущего пользователя.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Детали квеста.</returns>
    /// <response code="200">Детали квеста успешно получены.</response>
    /// <response code="400">Некорректный идентификатор квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден.</response>
    /// <response code="409">Квест недоступен и участия пользователя нет.</response>
    [HttpGet("{questId:long}")]
    [ProducesResponseType(typeof(QuestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuestDetailsDto>> GetById(
        long questId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (questId <= 0)
        {
            return BadRequest(new { message = "A valid quest ID is required." });
        }

        var result = await questService.GetDetailsAsync(
            questId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    /// <summary>
    /// Присоединиться к квесту.
    /// </summary>
    /// <remarks>
    /// Повторный запрос возвращает существующее участие и не создаёт дубликат.
    /// </remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Данные участия в квесте.</returns>
    /// <response code="200">Участие создано или уже существовало.</response>
    /// <response code="400">Некорректный идентификатор квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден.</response>
    /// <response code="409">Квест недоступен, уже завершён или участие отменено.</response>
    [HttpPost("{questId:long}/join")]
    [ProducesResponseType(typeof(JoinQuestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinQuestResultDto>> Join(
        long questId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (questId <= 0)
        {
            return BadRequest(new { message = "A valid quest ID is required." });
        }

        var result = await questService.JoinAsync(
            questId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    /// <summary>
    /// Завершить шаг квеста.
    /// </summary>
    /// <remarks>
    /// Для шага типа <c>proof</c> обязателен <c>storagePath</c>; доказательство получает статус
    /// <c>pending</c> и требует проверки. Шаги типа <c>automatic</c> пока не поддерживаются.
    /// Повторный запрос для выполненного шага не создаёт новое завершение.
    /// </remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага квеста.</param>
    /// <param name="request">Данные шага: путь к доказательству и комментарий.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние шага после выполнения.</returns>
    /// <response code="200">Шаг выполнен или уже был выполнен ранее.</response>
    /// <response code="400">Некорректные идентификаторы или отсутствует путь к доказательству.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест, шаг или участие не найдены.</response>
    /// <response code="409">Квест недоступен, уже завершён, доказательство уже на проверке
    /// либо шаг требует автоматического выполнения.</response>
    [HttpPost("{questId:long}/steps/{stepId:long}/complete")]
    [ProducesResponseType(typeof(QuestStepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuestStepResultDto>> CompleteStep(
        long questId,
        long stepId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        CompleteQuestStepRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (questId <= 0 || stepId <= 0)
        {
            return BadRequest(new { message = "Valid quest and step IDs are required." });
        }

        var result = await questService.CompleteStepAsync(
            questId,
            stepId,
            userId.Value,
            new CompleteQuestStepCommand(request?.StoragePath, request?.Comment),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    /// <summary>
    /// Завершить квест и получить награду.
    /// </summary>
    /// <remarks>
    /// Награда начисляется атомарно с завершением квеста только после выполнения всех
    /// обязательных шагов и одобрения необходимых доказательств. Повторный запрос
    /// не начисляет награду повторно.
    /// </remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат завершения квеста и начисления награды.</returns>
    /// <response code="200">Квест завершён или уже был завершён ранее.</response>
    /// <response code="400">Некорректный идентификатор квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден, участие отсутствует.</response>
    /// <response code="409">Квест недоступен, обязательные шаги не выполнены,
    /// доказательства не одобрены либо шаги требуют автоматического выполнения.</response>
    [HttpPost("{questId:long}/complete")]
    [ProducesResponseType(typeof(CompleteQuestResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompleteQuestResultDto>> Complete(
        long questId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (questId <= 0)
        {
            return BadRequest(new { message = "A valid quest ID is required." });
        }

        var result = await questService.CompleteAsync(
            questId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    /// <summary>
    /// Получить страницу квестов для административного просмотра.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех квестов.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список квестов успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("admin")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(PagedResult<AdminQuestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminQuestDto>>> GetForAdministration(
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

        var result = await questService.GetForAdministrationAsync(
            isActive,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<AdminQuestDto>(
            result.Items.Select(ToAdminDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Создать квест.
    /// </summary>
    /// <param name="request">Данные квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Квест успешно создан.</response>
    /// <response code="400">Некорректные данные квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminQuestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminQuestDto>> Create(
        [FromBody] CreateQuestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await questService.CreateAsync(
            ToCommand(
                request.Title,
                request.Description,
                request.RewardAmount,
                request.StartAt,
                request.EndAt,
                request.IsActive),
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { questId = result.Value!.Id },
            ToAdminDto(result.Value!));
    }

    /// <summary>
    /// Обновить квест.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="request">Данные квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Квест успешно обновлён.</response>
    /// <response code="400">Некорректные данные квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден.</response>
    [HttpPut("{questId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminQuestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminQuestDto>> Update(
        long questId,
        [FromBody] UpdateQuestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await questService.UpdateAsync(
            questId,
            ToCommand(
                request.Title,
                request.Description,
                request.RewardAmount,
                request.StartAt,
                request.EndAt,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToAdminDto(result.Value!));
    }

    /// <summary>
    /// Деактивировать квест.
    /// </summary>
    /// <remarks>Квест не удаляется физически: шаги, участия и доказательства сохраняются.</remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Квест деактивирован.</response>
    /// <response code="400">Некорректный идентификатор квеста.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден.</response>
    [HttpDelete("{questId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Deactivate(
        long questId,
        CancellationToken cancellationToken)
    {
        var result = await questService.DeactivateAsync(questId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Добавить шаг в квест.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="request">Данные шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Шаг успешно создан.</response>
    /// <response code="400">Некорректные данные шага.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест не найден.</response>
    [HttpPost("{questId:long}/steps")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminQuestStepDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminQuestStepDto>> CreateStep(
        long questId,
        [FromBody] CreateQuestStepRequest request,
        CancellationToken cancellationToken)
    {
        var result = await questService.CreateStepAsync(
            questId,
            ToCommand(
                request.Title,
                request.Description,
                request.CompletionType,
                request.SortOrder,
                request.IsRequired,
                request.CompletionData),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { questId },
            ToAdminStepDto(result.Value!));
    }

    /// <summary>
    /// Обновить шаг квеста.
    /// </summary>
    /// <remarks>
    /// Тип шага нельзя изменить, если участники уже выполняли этот шаг.
    /// </remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <param name="request">Данные шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Шаг успешно обновлён.</response>
    /// <response code="400">Некорректные данные шага.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест или шаг не найдены.</response>
    /// <response code="409">Тип шага нельзя изменить, потому что шаг уже выполнялся.</response>
    [HttpPut("{questId:long}/steps/{stepId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminQuestStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminQuestStepDto>> UpdateStep(
        long questId,
        long stepId,
        [FromBody] UpdateQuestStepRequest request,
        CancellationToken cancellationToken)
    {
        var result = await questService.UpdateStepAsync(
            questId,
            stepId,
            ToCommand(
                request.Title,
                request.Description,
                request.CompletionType,
                request.SortOrder,
                request.IsRequired,
                request.CompletionData),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToAdminStepDto(result.Value!));
    }

    /// <summary>
    /// Удалить шаг квеста.
    /// </summary>
    /// <remarks>Шаг нельзя удалить, если участники уже выполняли его.</remarks>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Шаг успешно удалён.</response>
    /// <response code="400">Некорректные идентификаторы.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Квест или шаг не найдены.</response>
    /// <response code="409">Шаг нельзя удалить, потому что он уже выполнялся.</response>
    [HttpDelete("{questId:long}/steps/{stepId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteStep(
        long questId,
        long stepId,
        CancellationToken cancellationToken)
    {
        var result = await questService.DeleteStepAsync(questId, stepId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Получить страницу доказательств для модерации.
    /// </summary>
    /// <remarks>Доказательства, ожидающие проверки, возвращаются первыми.</remarks>
    /// <param name="status">Фильтр по статусу: <c>pending</c>, <c>approved</c> или <c>rejected</c>.</param>
    /// <param name="questId">Фильтр по идентификатору квеста.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список доказательств успешно получен.</response>
    /// <response code="400">Некорректные параметры фильтра или пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("proofs")]
    [Authorize(Policy = AppPolicies.ModerateActivities)]
    [ProducesResponseType(typeof(PagedResult<QuestProofDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<QuestProofDto>>> GetProofs(
        [FromQuery] string? status = null,
        [FromQuery] long? questId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        var result = await questService.GetProofsAsync(
            status,
            questId,
            page,
            pageSize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(new PagedResult<QuestProofDto>(
            result.Value!.Items.Select(ToDto).ToList(),
            result.Value!.TotalCount,
            result.Value!.Page,
            result.Value!.PageSize));
    }

    /// <summary>
    /// Проверить доказательство выполнения шага квеста.
    /// </summary>
    /// <remarks>
    /// Повторная проверка с тем же статусом идемпотентна; попытка изменить
    /// уже принятое решение возвращает конфликт.
    /// </remarks>
    /// <param name="proofId">Идентификатор доказательства.</param>
    /// <param name="request">Новый статус доказательства.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Доказательство проверено.</response>
    /// <response code="400">Некорректный статус или идентификатор.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Доказательство не найдено.</response>
    /// <response code="409">Доказательство уже проверено с другим статусом.</response>
    [HttpPost("proofs/{proofId:long}/review")]
    [Authorize(Policy = AppPolicies.ModerateActivities)]
    [ProducesResponseType(typeof(ReviewQuestProofResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewQuestProofResultDto>> ReviewProof(
        long proofId,
        [FromBody] ReviewQuestProofRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await questService.ReviewProofAsync(
            proofId,
            new ReviewQuestProofCommand(request.Status),
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(new ReviewQuestProofResultDto(
            ToDto(result.Value!.Proof),
            result.Value!.AlreadyReviewed));
    }

    private static QuestListItemDto ToDto(QuestSummary summary) =>
        new(
            summary.Quest.Id,
            summary.Quest.Title,
            summary.Quest.Description,
            summary.Quest.RewardAmount,
            summary.Quest.StartAt,
            summary.Quest.EndAt,
            summary.StepsCount,
            summary.RequiredStepsCount,
            summary.ParticipationStatus);

    private static MyQuestDto ToDto(MyQuestSummary summary) =>
        new(
            summary.Participation.QuestId,
            summary.Participation.Quest.Title,
            summary.Participation.Quest.RewardAmount,
            summary.Participation.Status,
            summary.Participation.StartedAt,
            summary.Participation.CompletedAt,
            summary.Participation.RewardClaimed,
            summary.Participation.Quest.StartAt,
            summary.Participation.Quest.EndAt,
            summary.TotalSteps,
            summary.CompletedSteps);

    private static QuestDetailsDto ToDto(QuestDetailsResult details) =>
        new(
            details.Quest.Id,
            details.Quest.Title,
            details.Quest.Description,
            details.Quest.RewardAmount,
            details.Quest.IsActive,
            details.Quest.StartAt,
            details.Quest.EndAt,
            details.Participation is null
                ? null
                : new QuestParticipationDto(
                    details.Participation.Id,
                    details.Participation.Status,
                    details.Participation.StartedAt,
                    details.Participation.CompletedAt,
                    details.Participation.RewardClaimed),
            details.Steps.Select(ToDto).ToList());

    private static QuestStepDto ToDto(QuestStepState state)
    {
        var proof = state.LatestProof;

        var isCompleted = state.Step.CompletionType switch
        {
            QuestStepCompletionType.Manual => state.Completion is not null,
            QuestStepCompletionType.Proof => proof?.Status == QuestProofStatuses.Approved,
            _ => false
        };

        return new QuestStepDto(
            state.Step.Id,
            state.Step.Title,
            state.Step.Description,
            QuestStepCompletionTypeNames.ToDbValue(state.Step.CompletionType),
            state.Step.SortOrder,
            state.Step.IsRequired,
            isCompleted,
            state.Completion?.CompletedAt,
            proof?.Status,
            proof?.StoragePath,
            proof?.Comment);
    }

    private static JoinQuestResultDto ToDto(QuestJoinResult result) =>
        new(
            result.Participation.QuestId,
            result.Participation.Id,
            result.Participation.Status,
            result.Participation.StartedAt,
            result.AlreadyParticipating);

    private static QuestStepResultDto ToDto(QuestStepResult result) =>
        new(
            result.Completion.QuestId,
            result.Completion.StepId,
            result.CompletionType,
            result.Completed,
            result.ProofStatus,
            result.Completion.CompletedAt,
            result.AlreadyCompleted);

    private static CompleteQuestResultDto ToDto(QuestCompletionResult result) =>
        new(
            result.Participation.QuestId,
            result.Participation.Id,
            result.Participation.Status,
            result.Participation.CompletedAt,
            result.RewardAmount,
            result.BalanceAfter,
            result.RewardGranted,
            result.AlreadyCompleted);

    private static AdminQuestDto ToAdminDto(AdminQuestSummary summary) =>
        new(
            summary.Quest.Id,
            summary.Quest.Title,
            summary.Quest.Description,
            summary.Quest.RewardAmount,
            summary.Quest.IsActive,
            summary.Quest.StartAt,
            summary.Quest.EndAt,
            summary.StepsCount,
            summary.ParticipantsCount,
            summary.PendingProofsCount,
            summary.Quest.CreatedBy,
            summary.Quest.CreatedAt,
            summary.Quest.UpdatedAt);

    private static AdminQuestDto ToAdminDto(Quest quest) =>
        new(
            quest.Id,
            quest.Title,
            quest.Description,
            quest.RewardAmount,
            quest.IsActive,
            quest.StartAt,
            quest.EndAt,
            0,
            0,
            0,
            quest.CreatedBy,
            quest.CreatedAt,
            quest.UpdatedAt);

    private static AdminQuestStepDto ToAdminStepDto(QuestStep step) =>
        new(
            step.Id,
            step.QuestId,
            step.Title,
            step.Description,
            QuestStepCompletionTypeNames.ToDbValue(step.CompletionType),
            step.SortOrder,
            step.IsRequired,
            step.CompletionData,
            step.CreatedAt);

    private static QuestProofDto ToDto(QuestProofView view) =>
        new(
            view.Proof.Id,
            view.Proof.CompletionId,
            view.QuestId,
            view.QuestTitle,
            view.StepId,
            view.StepTitle,
            view.UserId,
            view.Proof.StoragePath,
            view.Proof.Comment,
            view.Proof.Status,
            view.Proof.ReviewedBy,
            view.Proof.ReviewedAt,
            view.Proof.CreatedAt);

    private static QuestCommand ToCommand(
        string? title,
        string? description,
        long? rewardAmount,
        DateTime? startAt,
        DateTime? endAt,
        bool isActive) =>
        new(title, description, rewardAmount, startAt, endAt, isActive);

    private static QuestStepCommand ToCommand(
        string? title,
        string? description,
        string? completionType,
        int? sortOrder,
        bool? isRequired,
        string? completionData) =>
        new(title, description, completionType, sortOrder ?? 0, isRequired ?? true, completionData);

    private static string? ValidatePaging(int page, int pageSize) =>
        page < 1
            ? "Page must be greater than or equal to 1."
            : pageSize is < 1 or > MaxPageSize
                ? $"PageSize must be between 1 and {MaxPageSize}."
                : null;
}
