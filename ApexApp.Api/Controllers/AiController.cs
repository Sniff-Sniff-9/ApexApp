using ApexApp.Api.Analytics;
using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Ai;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services.Ai;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Ассистент административной панели: черновики текстов и умные рекомендации.
/// </summary>
/// <remarks>
/// Модуль не сохраняет результаты и ничего не публикует: принятый текст администратор
/// переносит в существующие формы квестов, мероприятий и каталога наград. Статистику
/// для рекомендаций собирает Backend, модель не имеет доступа к базе данных.
/// </remarks>
[ApiController]
[Route("api/ai")]
[Authorize(Policy = AppPolicies.ActiveUser)]
[EnableRateLimiting(AiRateLimiting.PolicyName)]
public sealed class AiController(
    IAiContentService contentService,
    IAiRecommendationService recommendationService,
    IAnalyticsScopeResolver scopeResolver,
    IOptions<DeepSeekOptions> options) : ApiControllerBase
{
    /// <summary>
    /// Состояние модуля ИИ: настроена ли интеграция и какая модель используется.
    /// </summary>
    /// <returns>Состояние модуля без раскрытия ключа.</returns>
    /// <response code="200">Состояние получено.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("status")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<AiStatusDto> GetStatus()
    {
        var current = options.Value;

        var host = Uri.TryCreate(current.BaseUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "not configured";

        return Ok(new AiStatusDto(
            current.IsConfigured,
            current.Model,
            host,
            Math.Clamp(current.TimeoutSeconds, 5, 120)));
    }

    /// <summary>
    /// Черновики названия и описания квеста.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты названия и описания.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/quest-drafts")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<QuestDraftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<QuestDraftDto>>>> GenerateQuestDrafts(
        [FromBody] GenerateQuestDraftRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateQuestDraftsAsync(
            new AiQuestDraftRequest(
                request.Topic,
                request.Audience,
                request.RewardAmount,
                request.DurationDays,
                request.Tone,
                request.Variants),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<QuestDraftDto>>.Create(
            result.Value!.Content.Select(QuestDraftDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Черновики шагов квеста по его названию и описанию.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики шагов.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/quest-steps")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<QuestStepDraftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<QuestStepDraftDto>>>> GenerateQuestSteps(
        [FromBody] GenerateQuestStepsRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateQuestStepsAsync(
            new AiQuestStepRequest(
                request.QuestTitle,
                request.QuestDescription,
                request.StepsCount,
                request.ProofHint),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<QuestStepDraftDto>>.Create(
            result.Value!.Content.Select(QuestStepDraftDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Черновики названия и описания анкеты.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты названия и описания анкеты.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/survey-draft")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<SurveyDraftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<SurveyDraftDto>>>> GenerateSurveyDraft(
        [FromBody] GenerateSurveyDraftRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateSurveyDraftsAsync(
            new AiSurveyDraftRequest(
                request.Topic,
                request.Audience,
                request.RewardAmount,
                request.QuestionsCount,
                request.Tone,
                request.Variants),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<SurveyDraftDto>>.Create(
            result.Value!.Content.Select(SurveyDraftDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Черновики вопросов анкеты по её названию и описанию.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики вопросов с типами и вариантами ответа.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/survey-questions")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<SurveyQuestionDraftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<SurveyQuestionDraftDto>>>> GenerateSurveyQuestions(
        [FromBody] GenerateSurveyQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateSurveyQuestionsAsync(
            new AiSurveyQuestionsRequest(
                request.SurveyTitle,
                request.SurveyDescription,
                request.QuestionsCount,
                request.Audience),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<SurveyQuestionDraftDto>>.Create(
            result.Value!.Content.Select(SurveyQuestionDraftDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Черновики анонса корпоративного мероприятия.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты анонса.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/event-announcement")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<EventAnnouncementDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<EventAnnouncementDto>>>> GenerateEventAnnouncement(
        [FromBody] GenerateEventAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateEventAnnouncementsAsync(
            new AiEventAnnouncementRequest(
                request.Title,
                request.Location,
                request.StartAt,
                request.RewardAmount,
                request.MaxParticipants,
                request.Audience,
                request.Tone,
                request.Variants),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<EventAnnouncementDto>>.Create(
            result.Value!.Content.Select(EventAnnouncementDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Черновики названия и описания награды.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты описания награды.</returns>
    /// <response code="200">Черновики сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/reward-description")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<RewardDescriptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<RewardDescriptionDto>>>> GenerateRewardDescription(
        [FromBody] GenerateRewardDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateRewardDescriptionsAsync(
            new AiRewardDescriptionRequest(
                request.ItemName,
                request.Category,
                request.Price,
                request.Audience,
                request.Tone,
                request.Variants),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<RewardDescriptionDto>>.Create(
            result.Value!.Content.Select(RewardDescriptionDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Улучшение существующего текста.
    /// </summary>
    /// <param name="request">Параметры улучшения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Улучшенный текст и перечень изменений.</returns>
    /// <response code="200">Текст улучшен.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/improve")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<ImprovedTextDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<ImprovedTextDto>>> ImproveText(
        [FromBody] ImproveTextRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.ImproveTextAsync(
            new AiImproveTextRequest(
                request.Text,
                request.TextKind,
                request.Instruction,
                request.Tone),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<ImprovedTextDto>.Create(
            ImprovedTextDto.From(result.Value!.Content),
            result.Value!.Meta));
    }

    /// <summary>
    /// Альтернативные версии существующего текста.
    /// </summary>
    /// <param name="request">Параметры генерации.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Альтернативы с подписями.</returns>
    /// <response code="200">Альтернативы сгенерированы.</response>
    /// <response code="400">Некорректные параметры.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("content/alternatives")]
    [Authorize(Policy = AppPolicies.UseAiContent)]
    [ProducesResponseType(typeof(AiResultDto<IReadOnlyList<TextAlternativeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<IReadOnlyList<TextAlternativeDto>>>> GenerateAlternatives(
        [FromBody] GenerateAlternativesRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var result = await contentService.GenerateAlternativesAsync(
            new AiAlternativesRequest(
                request.Text,
                request.TextKind,
                request.Tone,
                request.Variants),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<IReadOnlyList<TextAlternativeDto>>.Create(
            result.Value!.Content.Select(TextAlternativeDto.From).ToList(),
            result.Value!.Meta));
    }

    /// <summary>
    /// Анализ вовлечённости и рекомендации по активностям и наградам.
    /// </summary>
    /// <param name="request">Параметры окна и фокус рекомендаций.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Показатели, анализ и идеи.</returns>
    /// <response code="200">Рекомендации сформированы.</response>
    /// <response code="400">Некорректные параметры окна или фокуса.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав для запрошенного отдела.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("recommendations/engagement")]
    [Authorize(Policy = AppPolicies.UseAiEngagement)]
    [ProducesResponseType(typeof(AiResultDto<EngagementRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<EngagementRecommendationDto>>> GetEngagementRecommendations(
        [FromBody] AiEngagementRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        var scope = await scopeResolver.ResolveAsync(User, request.DepartmentId, cancellationToken);

        if (!scope.Allowed)
        {
            return ToErrorResult(scope.StatusCode, scope.Error);
        }

        if (!AiRecommendationFocusCodes.TryParse(request.Focus, out var focus))
        {
            return BadRequest(new { message = "Focus must be one of: all, activities, rewards." });
        }

        if (!TryCreateQuery(
                request.From,
                request.To,
                request.Granularity,
                request.Top,
                scope.DepartmentId,
                out var query,
                out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await recommendationService.GetEngagementRecommendationsAsync(
            new AiEngagementRecommendationCommand(query, focus, request.Notes),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<EngagementRecommendationDto>.Create(
            EngagementRecommendationDto.From(result.Value!),
            result.Value!.Meta));
    }

    /// <summary>
    /// Анализ спроса и рекомендации по каталогу наград для мерчандайзера.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Показатели спроса, анализ, идеи товаров и действия по каталогу.</returns>
    /// <response code="200">Рекомендации сформированы.</response>
    /// <response code="400">Некорректные параметры окна.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="429">Превышено ограничение частоты запросов.</response>
    /// <response code="502">Провайдер вернул ошибку или непригодный ответ.</response>
    /// <response code="503">Модуль не настроен.</response>
    /// <response code="504">Провайдер не ответил вовремя.</response>
    [HttpPost("recommendations/products")]
    [Authorize(Policy = AppPolicies.UseAiMerchandising)]
    [ProducesResponseType(typeof(AiResultDto<ProductRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AiResultDto<ProductRecommendationDto>>> GetProductRecommendations(
        [FromBody] AiProductRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.GetUserId();

        if (adminId is null)
        {
            return Unauthorized();
        }

        if (!TryCreateQuery(
                request.From,
                request.To,
                request.Granularity,
                request.Top,
                departmentId: null,
                out var query,
                out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await recommendationService.GetProductRecommendationsAsync(
            new AiProductRecommendationCommand(query, request.Notes),
            adminId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(AiResultDto<ProductRecommendationDto>.Create(
            ProductRecommendationDto.From(result.Value!),
            result.Value!.Meta));
    }

    /// <summary>
    /// Создаёт проверенную модель аналитического запроса из параметров запроса ИИ.
    /// </summary>
    private static bool TryCreateQuery(
        DateTime? from,
        DateTime? to,
        string? granularity,
        int top,
        int? departmentId,
        out AnalyticsQuery query,
        out string? error)
    {
        var analyticsRequest = new AnalyticsQueryRequest
        {
            From = from,
            To = to,
            Granularity = granularity,
            DepartmentId = departmentId,
            Top = top
        };

        return AnalyticsQueryBinder.TryCreate(
            analyticsRequest,
            departmentId,
            out query,
            out error);
    }
}
