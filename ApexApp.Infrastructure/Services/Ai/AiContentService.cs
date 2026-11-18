using System.Collections;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services.Quests;
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
    IQuestStepActionOptionProvider optionProvider,
    ICategoryService categoryService,
    ICatalogService catalogService,
    IOptions<DeepSeekOptions> options,
    ILogger<AiContentService> logger) : IAiContentService
{
    private readonly DeepSeekOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<QuestTextDraft>>>> GenerateQuestDraftsAsync(
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
            return ServiceResult<AiContentResult<IReadOnlyList<QuestTextDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<QuestDraftsPayload, IReadOnlyList<QuestTextDraft>>(
            "quest-drafts",
            adminId,
            completion.Value!,
            payload => AiContentValidator.QuestDrafts(payload.Variants, variants));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<QuestDraft>>> GenerateQuestActivityDraftAsync(
        AiQuestActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ServiceResult<AiContentResult<QuestDraft>>.Fail(400, "Prompt is required.");
        }

        if (request.RewardAmount is null)
        {
            return ServiceResult<AiContentResult<QuestDraft>>.Fail(
                400,
                "RewardAmount is required.");
        }

        if (request.RewardAmount < 0)
        {
            return ServiceResult<AiContentResult<QuestDraft>>.Fail(
                400,
                "RewardAmount cannot be negative.");
        }

        if (request.StartAt is not null
            && request.EndAt is not null
            && request.EndAt <= request.StartAt)
        {
            return ServiceResult<AiContentResult<QuestDraft>>.Fail(
                400,
                "EndAt must be later than StartAt.");
        }

        var steps = Math.Clamp(request.StepsCount, 1, AiContentValidator.MaxSteps);
        var candidates = request.AllowAutomatic
            ? await optionProvider.GetCandidateSetsAsync(cancellationToken)
            : null;

        var prompt = AiPromptBuilder.QuestActivityDraft(
            request with { StepsCount = steps },
            candidates);

        var completion = await executor.ExecuteAsync(
            "quest-activity-draft",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<QuestDraft>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<QuestActivityDraftPayload, QuestDraft>(
            "quest-activity-draft",
            adminId,
            completion.Value!,
            payload => AiContentValidator.QuestActivityDraft(
                payload,
                request with { StepsCount = steps },
                candidates));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<EventDraft>>> GenerateEventActivityDraftAsync(
        AiEventActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(400, "Prompt is required.");
        }

        if (request.StartAt is null || request.EndAt is null)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                400,
                "StartAt and EndAt are required.");
        }

        if (request.EndAt <= request.StartAt)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                400,
                "EndAt must be later than StartAt.");
        }

        if (request.MaxParticipants is <= 0)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                400,
                "MaxParticipants must be greater than zero.");
        }

        if (request.RewardAmount is null)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                400,
                "RewardAmount is required.");
        }

        if (request.RewardAmount < 0)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                400,
                "RewardAmount cannot be negative.");
        }

        var prompt = AiPromptBuilder.EventActivityDraft(request);

        var completion = await executor.ExecuteAsync(
            "event-activity-draft",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<EventDraft>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<EventActivityDraftPayload, EventDraft>(
            "event-activity-draft",
            adminId,
            completion.Value!,
            payload => AiContentValidator.EventActivityDraft(payload, request));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<SurveyDraft>>> GenerateSurveyActivityDraftAsync(
        AiSurveyActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ServiceResult<AiContentResult<SurveyDraft>>.Fail(400, "Prompt is required.");
        }

        if (request.RewardAmount is null)
        {
            return ServiceResult<AiContentResult<SurveyDraft>>.Fail(
                400,
                "RewardAmount is required.");
        }

        if (request.RewardAmount < 0)
        {
            return ServiceResult<AiContentResult<SurveyDraft>>.Fail(
                400,
                "RewardAmount cannot be negative.");
        }

        if (request.StartAt is not null
            && request.EndAt is not null
            && request.EndAt <= request.StartAt)
        {
            return ServiceResult<AiContentResult<SurveyDraft>>.Fail(
                400,
                "EndAt must be later than StartAt.");
        }

        var questions = Math.Clamp(
            request.QuestionsCount,
            1,
            AiContentValidator.MaxSurveyQuestions);

        var prompt = AiPromptBuilder.SurveyActivityDraft(
            request with { QuestionsCount = questions });

        var completion = await executor.ExecuteAsync(
            "survey-activity-draft",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<SurveyDraft>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<SurveyActivityDraftPayload, SurveyDraft>(
            "survey-activity-draft",
            adminId,
            completion.Value!,
            payload => AiContentValidator.SurveyActivityDraft(
                payload,
                request with { QuestionsCount = questions }));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<ProductDraft>>> GenerateProductDraftAsync(
        AiProductDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ServiceResult<AiContentResult<ProductDraft>>.Fail(400, "Prompt is required.");
        }

        if (request.Price is < 0)
        {
            return ServiceResult<AiContentResult<ProductDraft>>.Fail(
                400,
                "Price cannot be negative.");
        }

        if (request.Stock is < 0)
        {
            return ServiceResult<AiContentResult<ProductDraft>>.Fail(
                400,
                "Stock cannot be negative.");
        }

        var categories = (await categoryService.GetAllAsync(cancellationToken))
            .Select(category => new CategoryOption(category.Id, category.Name))
            .ToList();

        if (request.CategoryId is int categoryId
            && categories.All(category => category.Id != categoryId))
        {
            return ServiceResult<AiContentResult<ProductDraft>>.Fail(
                400,
                "Category was not found.");
        }

        var (referenceMin, referenceMax) = await GetPriceReferenceAsync(
            request.CategoryId,
            cancellationToken);

        var prompt = AiPromptBuilder.ProductDraft(request, categories);

        var completion = await executor.ExecuteAsync(
            "product-draft",
            AiPromptCatalog.ContentSystem,
            prompt,
            _options.ContentTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<AiContentResult<ProductDraft>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        var allowedCategoryIds = categories
            .Select(category => category.Id)
            .ToHashSet();

        return Parse<ProductDraftPayload, ProductDraft>(
            "product-draft",
            adminId,
            completion.Value!,
            payload => AiContentValidator.ProductActivityDraft(
                payload,
                request,
                allowedCategoryIds,
                referenceMin,
                referenceMax));
    }

    /// <summary>
    /// Возвращает реальный диапазон цен активных товаров категории.
    /// </summary>
    private async Task<(long? Min, long? Max)> GetPriceReferenceAsync(
        int? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId is not int id)
        {
            return (null, null);
        }

        var products = await catalogService.GetAllAsync(id, cancellationToken);
        var prices = products
            .Where(product => product.IsActive && !product.IsDigital)
            .Select(product => product.FinalPrice)
            .ToList();

        return prices.Count == 0
            ? (null, null)
            : (prices.Min(), prices.Max());
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

        // Список объектов загружает только Backend: модель не может придумать ID,
        // а клиент не может подменить допустимый набор.
        var candidates = request.AllowAutomatic
            ? await optionProvider.GetCandidateSetsAsync(cancellationToken)
            : null;

        var prompt = AiPromptBuilder.QuestSteps(
            request with { StepsCount = steps },
            candidates,
            request.AllowAutomatic);

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
            payload => AiContentValidator.QuestSteps(
                payload.Steps,
                steps,
                candidates,
                request.AllowAutomatic));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyTextDraft>>>> GenerateSurveyDraftsAsync(
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
            return ServiceResult<AiContentResult<IReadOnlyList<SurveyTextDraft>>>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        return Parse<SurveyDraftsPayload, IReadOnlyList<SurveyTextDraft>>(
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
