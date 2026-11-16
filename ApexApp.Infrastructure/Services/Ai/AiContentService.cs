using System.Collections;
using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Генерация черновиков для административной панели. Сервис не пишет в базу данных:
/// он возвращает только текст, который администратор может принять, изменить или
/// отклонить. Публикация выполняется существующими эндпоинтами квестов и мероприятий.
/// </summary>
public sealed class AiContentService(
    AiCompletionExecutor executor,
    IOptions<DeepSeekOptions> options,
    ILogger<AiContentService> logger) : IAiContentService
{
    private readonly DeepSeekOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<QuestDraft>>>> GenerateQuestDraftsAsync(
        AiQuestDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);
        var prompt = AiPromptBuilder.QuestDrafts(request with { Variants = variants });

        var completion = await executor.ExecuteAsync(
            "quest-drafts",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<QuestDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<QuestDraftsPayload, IReadOnlyList<QuestDraft>>(
            "quest-drafts",
            adminId,
            completion.Value!,
            payload => AiContentValidator.QuestDrafts(payload.Variants, variants));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<QuestStepDraft>>>> GenerateQuestStepsAsync(
        AiQuestStepRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.QuestTitle))
        {
            return ServiceResult<AiContentResult<IReadOnlyList<QuestStepDraft>>>.Fail(
                400,
                "Quest title is required.");
        }

        var steps = Math.Clamp(request.StepsCount, 1, AiContentValidator.MaxSteps);
        var prompt = AiPromptBuilder.QuestSteps(request with { StepsCount = steps });

        var completion = await executor.ExecuteAsync(
            "quest-steps",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<QuestStepDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<QuestStepsPayload, IReadOnlyList<QuestStepDraft>>(
            "quest-steps",
            adminId,
            completion.Value!,
            payload => AiContentValidator.QuestSteps(payload.Steps, steps));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyDraft>>>> GenerateSurveyDraftsAsync(
        AiSurveyDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);
        var prompt = AiPromptBuilder.SurveyDraft(request with { Variants = variants });

        var completion = await executor.ExecuteAsync(
            "survey-drafts",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<SurveyDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<SurveyDraftsPayload, IReadOnlyList<SurveyDraft>>(
            "survey-drafts",
            adminId,
            completion.Value!,
            payload => AiContentValidator.SurveyDrafts(payload.Variants, variants));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyQuestionDraft>>>> GenerateSurveyQuestionsAsync(
        AiSurveyQuestionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SurveyTitle))
        {
            return ServiceResult<AiContentResult<IReadOnlyList<SurveyQuestionDraft>>>.Fail(
                400,
                "Survey title is required.");
        }

        var questions = Math.Clamp(
            request.QuestionsCount,
            1,
            AiContentValidator.MaxSurveyQuestions);
        var prompt = AiPromptBuilder.SurveyQuestions(request with { QuestionsCount = questions });

        var completion = await executor.ExecuteAsync(
            "survey-questions",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<SurveyQuestionDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<SurveyQuestionsPayload, IReadOnlyList<SurveyQuestionDraft>>(
            "survey-questions",
            adminId,
            completion.Value!,
            payload => AiContentValidator.SurveyQuestions(payload.Questions, questions));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<EventAnnouncement>>>> GenerateEventAnnouncementsAsync(
        AiEventAnnouncementRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ServiceResult<AiContentResult<IReadOnlyList<EventAnnouncement>>>.Fail(
                400,
                "Event title is required.");
        }

        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);
        var prompt = AiPromptBuilder.EventAnnouncement(request with { Variants = variants });

        var completion = await executor.ExecuteAsync(
            "event-announcement",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<EventAnnouncement>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<EventAnnouncementsPayload, IReadOnlyList<EventAnnouncement>>(
            "event-announcement",
            adminId,
            completion.Value!,
            payload => AiContentValidator.EventAnnouncements(payload.Variants, variants));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<RewardDescription>>>> GenerateRewardDescriptionsAsync(
        AiRewardDescriptionRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ItemName))
        {
            return ServiceResult<AiContentResult<IReadOnlyList<RewardDescription>>>.Fail(
                400,
                "Reward name is required.");
        }

        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);
        var prompt = AiPromptBuilder.RewardDescription(request with { Variants = variants });

        var completion = await executor.ExecuteAsync(
            "reward-description",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<RewardDescription>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<RewardDescriptionsPayload, IReadOnlyList<RewardDescription>>(
            "reward-description",
            adminId,
            completion.Value!,
            payload => AiContentValidator.RewardDescriptions(payload.Variants, variants));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<ImprovedText>>> ImproveTextAsync(
        AiImproveTextRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return ServiceResult<AiContentResult<ImprovedText>>.Fail(400, "Text is required.");
        }

        if (request.Text.Length > AiContentValidator.ImprovedTextMaxLength)
        {
            return ServiceResult<AiContentResult<ImprovedText>>.Fail(
                400,
                $"Text must not exceed {AiContentValidator.ImprovedTextMaxLength} characters.");
        }

        var prompt = AiPromptBuilder.Improve(request);

        var completion = await executor.ExecuteAsync(
            "improve-text",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<ImprovedText>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<ImprovedTextPayload, ImprovedText>(
            "improve-text",
            adminId,
            completion.Value!,
            AiContentValidator.ImprovedText);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<TextAlternative>>>> GenerateAlternativesAsync(
        AiAlternativesRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return ServiceResult<AiContentResult<IReadOnlyList<TextAlternative>>>.Fail(400, "Text is required.");
        }

        if (request.Text.Length > AiContentValidator.ImprovedTextMaxLength)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<TextAlternative>>>.Fail(
                400,
                $"Text must not exceed {AiContentValidator.ImprovedTextMaxLength} characters.");
        }

        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);
        var prompt = AiPromptBuilder.Alternatives(request with { Variants = variants });

        var completion = await executor.ExecuteAsync(
            "text-alternatives",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<IReadOnlyList<TextAlternative>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<TextAlternativesPayload, IReadOnlyList<TextAlternative>>(
            "text-alternatives",
            adminId,
            completion.Value!,
            payload => AiContentValidator.Alternatives(payload.Variants, variants));
    }

    /// <summary>
    /// Разбирает ответ модели, нормализует содержимое и заполняет метрики.
    /// </summary>
    private ServiceResult<AiContentResult<T>> Parse<TPayload, T>(
        string kind,
        Guid adminId,
        AiCompletion completion,
        Func<TPayload, T?> project)
        where TPayload : class
        where T : class
    {
        if (!AiJson.TryDeserialize<TPayload>(completion.Content, out var payload, out var error)
            || payload is null)
        {
            logger.LogWarning(
                "AI {Kind} returned unusable JSON for admin {AdminId}: {Reason}.",
                kind,
                adminId,
                error);

            return ServiceResult<AiContentResult<T>>.Fail(502, AiMessages.InvalidResponse);
        }

        var content = project(payload);

        if (content is null || content is ICollection { Count: 0 })
        {
            logger.LogWarning(
                "AI {Kind} returned no usable content for admin {AdminId}.",
                kind,
                adminId);

            return ServiceResult<AiContentResult<T>>.Fail(502, AiMessages.EmptyResponse);
        }

        return ServiceResult<AiContentResult<T>>.Ok(
            new AiContentResult<T>(content, AiGenerationMeta.Create(completion)));
    }
}
