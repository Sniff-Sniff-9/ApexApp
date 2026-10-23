using System.Text.Json;
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
/// Пользовательские сценарии анкет: список, детали, отправка ответов и просмотр
/// собственных ответов, а также администрирование анкет, вопросов и результатов.
/// </summary>
/// <param name="surveyService">Сервис анкет.</param>
[ApiController]
[Route("api/surveys")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class SurveysController(ISurveyService surveyService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Получить страницу активных анкет, доступных в текущий момент.
    /// </summary>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список анкет успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SurveyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SurveyListItemDto>>> GetAll(
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

        var result = await surveyService.GetAvailableAsync(
            userId.Value,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<SurveyListItemDto>(
            result.Items.Select(ToListItemDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Получить детали анкеты вместе с вопросами и ответами текущего пользователя.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Детали анкеты успешно получены.</response>
    /// <response code="400">Некорректный идентификатор анкеты.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    /// <response code="409">Анкета неактивна или вне дат проведения, а ответов пользователя нет.</response>
    [HttpGet("{surveyId:long}")]
    [ProducesResponseType(typeof(SurveyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SurveyDetailsDto>> GetById(
        long surveyId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await surveyService.GetDetailsAsync(
            surveyId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDetailsDto(result.Value!));
    }

    /// <summary>
    /// Отправить ответы на анкету.
    /// </summary>
    /// <remarks>
    /// Проверяются типы ответов, обязательные вопросы и правило повторных ответов
    /// (<c>allow_multiple_responses</c>).
    /// </remarks>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="request">Ответы на вопросы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Ответы успешно сохранены.</response>
    /// <response code="400">Некорректные ответы: неизвестный вопрос, неверный тип значения или пропущен обязательный вопрос.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    /// <response code="409">Анкета закрыта или ответ уже отправлен.</response>
    [HttpPost("{surveyId:long}/responses")]
    [ProducesResponseType(typeof(SubmitSurveyResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitSurveyResultDto>> Submit(
        long surveyId,
        [FromBody] SubmitSurveyRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (request?.Answers is null)
        {
            return BadRequest(new { message = "Answers are required." });
        }

        var answers = request.Answers
            .Select(answer => new SurveyAnswerCommand(
                answer.QuestionId,
                answer.Value ?? default))
            .ToList();

        var result = await surveyService.SubmitAsync(
            surveyId,
            userId.Value,
            new SubmitSurveyCommand(answers),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var submission = result.Value!;

        return StatusCode(
            StatusCodes.Status201Created,
            new SubmitSurveyResultDto(
                submission.Response.SurveyId,
                submission.Response.Id,
                submission.Response.SubmittedAt,
                submission.AnswersCount));
    }

    /// <summary>
    /// Получить ответы текущего пользователя на анкету.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Ответы успешно получены.</response>
    /// <response code="400">Некорректный идентификатор анкеты.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    [HttpGet("{surveyId:long}/my-responses")]
    [ProducesResponseType(typeof(IReadOnlyList<SurveyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SurveyResponseDto>>> GetMyResponses(
        long surveyId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await surveyService.GetMyResponsesAsync(
            surveyId,
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(result.Value!.Select(ToResponseDto).ToList());
    }

    /// <summary>
    /// Получить страницу анкет для административного просмотра.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех анкет.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы (от 1 до 100).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Список анкет успешно получен.</response>
    /// <response code="400">Некорректные параметры пагинации.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("admin")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(PagedResult<AdminSurveyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminSurveyDto>>> GetForAdministration(
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

        var result = await surveyService.GetForAdministrationAsync(
            isActive,
            page,
            pageSize,
            cancellationToken);

        return Ok(new PagedResult<AdminSurveyDto>(
            result.Items.Select(ToAdminDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    /// <summary>
    /// Создать анкету.
    /// </summary>
    /// <param name="request">Данные анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Анкета успешно создана.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(AdminSurveyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminSurveyDto>> Create(
        [FromBody] CreateSurveyRequest? request,
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

        var result = await surveyService.CreateAsync(
            new SurveyCommand(
                request.Title,
                request.Description,
                request.StartAt,
                request.EndAt,
                request.AllowMultipleResponses,
                request.IsActive),
            userId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var dto = ToAdminDto(result.Value!);

        return CreatedAtAction(nameof(GetById), new { surveyId = dto.Id }, dto);
    }

    /// <summary>
    /// Обновить анкету.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="request">Данные анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Анкета успешно обновлена.</response>
    /// <response code="400">Некорректные данные запроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    [HttpPut("{surveyId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        long surveyId,
        [FromBody] UpdateSurveyRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await surveyService.UpdateAsync(
            surveyId,
            new SurveyCommand(
                request.Title,
                request.Description,
                request.StartAt,
                request.EndAt,
                request.AllowMultipleResponses,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Деактивировать анкету.
    /// </summary>
    /// <remarks>
    /// Анкета не удаляется физически: вопросы и ответы сохраняются.
    /// </remarks>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Анкета деактивирована.</response>
    /// <response code="400">Некорректный идентификатор анкеты.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    [HttpDelete("{surveyId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Deactivate(
        long surveyId,
        CancellationToken cancellationToken)
    {
        var result = await surveyService.DeactivateAsync(surveyId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Добавить вопрос в анкету.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="request">Данные вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="201">Вопрос успешно добавлен.</response>
    /// <response code="400">Некорректные данные вопроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    [HttpPost("{surveyId:long}/questions")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(SurveyQuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyQuestionDto>> CreateQuestion(
        long surveyId,
        [FromBody] CreateSurveyQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await surveyService.CreateQuestionAsync(
            surveyId,
            ToCommand(
                request.QuestionText,
                request.QuestionType,
                request.Options,
                request.IsRequired,
                request.SortOrder),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ToQuestionDto(result.Value!));
    }

    /// <summary>
    /// Обновить вопрос анкеты.
    /// </summary>
    /// <remarks>
    /// Тип вопроса и варианты ответа нельзя изменить, если на вопрос уже есть ответы.
    /// </remarks>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="questionId">Идентификатор вопроса.</param>
    /// <param name="request">Данные вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Вопрос успешно обновлён.</response>
    /// <response code="400">Некорректные данные вопроса.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета или вопрос не найдены.</response>
    /// <response code="409">На вопрос уже есть ответы, поэтому тип и варианты нельзя изменить.</response>
    [HttpPut("{surveyId:long}/questions/{questionId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateQuestion(
        long surveyId,
        long questionId,
        [FromBody] UpdateSurveyQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await surveyService.UpdateQuestionAsync(
            surveyId,
            questionId,
            ToCommand(
                request.QuestionText,
                request.QuestionType,
                request.Options,
                request.IsRequired,
                request.SortOrder),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Удалить вопрос анкеты.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="questionId">Идентификатор вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="204">Вопрос успешно удалён.</response>
    /// <response code="400">Некорректные идентификаторы.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета или вопрос не найдены.</response>
    /// <response code="409">На вопрос уже есть ответы, поэтому удаление невозможно.</response>
    [HttpDelete("{surveyId:long}/questions/{questionId:long}")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteQuestion(
        long surveyId,
        long questionId,
        CancellationToken cancellationToken)
    {
        var result = await surveyService.DeleteQuestionAsync(
            surveyId,
            questionId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Получить агрегированные результаты анкеты.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <response code="200">Результаты успешно получены.</response>
    /// <response code="400">Некорректный идентификатор анкеты.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Анкета не найдена.</response>
    [HttpGet("{surveyId:long}/results")]
    [Authorize(Policy = AppPolicies.ManageActivities)]
    [ProducesResponseType(typeof(SurveyResultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SurveyResultsDto>> GetResults(
        long surveyId,
        CancellationToken cancellationToken)
    {
        var result = await surveyService.GetResultsAsync(surveyId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToResultsDto(result.Value!));
    }

    private static SurveyListItemDto ToListItemDto(SurveySummary summary) =>
        new(
            summary.Survey.Id,
            summary.Survey.Title,
            summary.Survey.Description,
            summary.Survey.StartAt,
            summary.Survey.EndAt,
            summary.Survey.AllowMultipleResponses,
            summary.QuestionsCount,
            summary.HasResponded,
            summary.CanRespond,
            summary.LastSubmittedAt);

    private static SurveyDetailsDto ToDetailsDto(SurveyDetailsResult details) =>
        new(
            details.Survey.Id,
            details.Survey.Title,
            details.Survey.Description,
            details.Survey.IsActive,
            details.Survey.StartAt,
            details.Survey.EndAt,
            details.Survey.AllowMultipleResponses,
            details.CanRespond,
            details.Questions.Select(ToQuestionDto).ToList(),
            details.Responses.Select(ToResponseDto).ToList());

    private static SurveyQuestionDto ToQuestionDto(SurveyQuestionView view) =>
        new(
            view.Question.Id,
            view.Question.QuestionText,
            view.QuestionType,
            view.Options,
            view.Question.IsRequired,
            view.Question.SortOrder);

    private static SurveyQuestionDto ToQuestionDto(SurveyQuestion question) =>
        new(
            question.Id,
            question.QuestionText,
            SurveyQuestionTypes.Normalize(question.QuestionType),
            SurveyQuestionTypes.ParseOptions(question.Options),
            question.IsRequired,
            question.SortOrder);

    private static SurveyResponseDto ToResponseDto(SurveyResponseView view) =>
        new(
            view.Response.Id,
            view.Response.SurveyId,
            view.Response.UserId,
            view.Response.SubmittedAt,
            view.Answers
                .Select(answer => new SurveyAnswerDto(answer.QuestionId, ParseValue(answer.Answer)))
                .ToList());

    private static JsonElement? ParseValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AdminSurveyDto ToAdminDto(AdminSurveySummary summary) =>
        new(
            summary.Survey.Id,
            summary.Survey.Title,
            summary.Survey.Description,
            summary.Survey.IsActive,
            summary.Survey.StartAt,
            summary.Survey.EndAt,
            summary.Survey.AllowMultipleResponses,
            summary.QuestionsCount,
            summary.ResponsesCount,
            summary.Survey.CreatedBy,
            summary.Survey.CreatedAt,
            summary.Survey.UpdatedAt);

    private static AdminSurveyDto ToAdminDto(Survey survey) =>
        new(
            survey.Id,
            survey.Title,
            survey.Description,
            survey.IsActive,
            survey.StartAt,
            survey.EndAt,
            survey.AllowMultipleResponses,
            0,
            0,
            survey.CreatedBy,
            survey.CreatedAt,
            survey.UpdatedAt);

    private static SurveyResultsDto ToResultsDto(SurveyResultsResult results) =>
        new(
            results.Survey.Id,
            results.Survey.Title,
            results.ResponseCount,
            results.Questions
                .Select(question => new SurveyQuestionResultDto(
                    question.QuestionId,
                    question.QuestionText,
                    question.QuestionType,
                    question.AnswerCount,
                    question.OptionCounts
                        .Select(option => new SurveyOptionCountDto(option.Option, option.Count))
                        .ToList(),
                    question.AverageValue))
                .ToList());

    private static SurveyQuestionCommand ToCommand(
        string? questionText,
        string? questionType,
        IReadOnlyList<string>? options,
        bool? isRequired,
        int? sortOrder) =>
        new(questionText, questionType, options, isRequired ?? true, sortOrder ?? 0);

    private static string? ValidatePaging(int page, int pageSize) =>
        page < 1
            ? "Page must be greater than or equal to 1."
            : pageSize is < 1 or > MaxPageSize
                ? $"PageSize must be between 1 and {MaxPageSize}."
                : null;
}
