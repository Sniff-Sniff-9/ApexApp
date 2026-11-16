using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Умные рекомендации. Backend собирает статистику существующими аналитическими
/// сервисами, модель интерпретирует её и предлагает идеи, а решение принимает
/// администратор или мерчандайзер. Результаты не сохраняются.
/// </summary>
public sealed class AiRecommendationService(
    AiStatisticsDigestBuilder digestBuilder,
    AiCompletionExecutor executor,
    IOptions<DeepSeekOptions> options,
    ILogger<AiRecommendationService> logger) : IAiRecommendationService
{
    private const int MaxAnalysisLength = 1500;
    private const int MaxIdeaTextLength = 800;
    private const int MaxPriceBandLength = 40;
    private const int MaxActions = 5;
    private const int MaxActionLength = 300;
    private const int MaxTags = 4;
    private const int MaxTagLength = 40;

    private readonly DeepSeekOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<EngagementRecommendationReport>> GetEngagementRecommendationsAsync(
        AiEngagementRecommendationCommand command,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var digestResult = await digestBuilder.BuildEngagementAsync(command.Query, cancellationToken);

        if (!digestResult.IsSuccess)
        {
            return ServiceResult<EngagementRecommendationReport>.Fail(
                digestResult.StatusCode,
                digestResult.Error!);
        }

        var digest = digestResult.Value!;
        var prompt = AiPromptBuilder.EngagementRecommendations(
            digest.Text,
            command.Focus,
            command.Notes,
            AiContentValidator.MaxActivityIdeas,
            AiContentValidator.MaxRewardIdeas);

        var completion = await executor.ExecuteAsync(
            "engagement-recommendations",
            AiPromptCatalog.AnalysisSystem,
            prompt,
            _options.AnalysisTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<EngagementRecommendationReport>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        if (!AiJson.TryDeserialize<EngagementRecommendationsPayload>(
                completion.Value!.Content,
                out var payload,
                out var error)
            || payload is null)
        {
            logger.LogWarning(
                "AI engagement recommendations returned unusable JSON for admin {AdminId}: {Reason}.",
                adminId,
                error);

            return ServiceResult<EngagementRecommendationReport>.Fail(502, AiMessages.InvalidResponse);
        }

        IReadOnlyList<ActivityIdea> ideas = command.Focus == AiRecommendationFocus.Rewards
            ? Array.Empty<ActivityIdea>()
            : BuildActivityIdeas(payload.ActivityIdeas);

        IReadOnlyList<RewardIdea> rewards = command.Focus == AiRecommendationFocus.Activities
            ? Array.Empty<RewardIdea>()
            : BuildRewardIdeas(payload.RewardIdeas);

        var analysis = AiContentValidator.Text(payload.Analysis, MaxAnalysisLength) ?? string.Empty;

        if (analysis.Length == 0 && ideas.Count == 0 && rewards.Count == 0)
        {
            logger.LogWarning(
                "AI engagement recommendations returned no usable content for admin {AdminId}.",
                adminId);

            return ServiceResult<EngagementRecommendationReport>.Fail(502, AiMessages.EmptyResponse);
        }

        var report = new EngagementRecommendationReport(
            digest.Snapshot,
            analysis,
            digest.Findings,
            ideas,
            rewards,
            Merge(digest.Limitations, payload.DataLimitations),
            AiGenerationMeta.Create(completion.Value!));

        return ServiceResult<EngagementRecommendationReport>.Ok(report);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ProductRecommendationReport>> GetProductRecommendationsAsync(
        AiProductRecommendationCommand command,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var digestResult = await digestBuilder.BuildProductsAsync(command.Query, cancellationToken);

        if (!digestResult.IsSuccess)
        {
            return ServiceResult<ProductRecommendationReport>.Fail(
                digestResult.StatusCode,
                digestResult.Error!);
        }

        var digest = digestResult.Value!;
        var prompt = AiPromptBuilder.ProductRecommendations(
            digest.Text,
            command.Notes,
            AiContentValidator.MaxProductIdeas);

        var completion = await executor.ExecuteAsync(
            "product-recommendations",
            AiPromptCatalog.AnalysisSystem,
            prompt,
            _options.AnalysisTemperature,
            adminId,
            cancellationToken);

        if (!completion.IsSuccess)
        {
            return ServiceResult<ProductRecommendationReport>.Fail(
                completion.StatusCode,
                completion.Error!);
        }

        if (!AiJson.TryDeserialize<ProductRecommendationsPayload>(
                completion.Value!.Content,
                out var payload,
                out var error)
            || payload is null)
        {
            logger.LogWarning(
                "AI product recommendations returned unusable JSON for admin {AdminId}: {Reason}.",
                adminId,
                error);

            return ServiceResult<ProductRecommendationReport>.Fail(502, AiMessages.InvalidResponse);
        }

        var ideas = BuildProductIdeas(payload.ProductIdeas);
        var actions = AiContentValidator.StringList(payload.CatalogActions, MaxActions, MaxActionLength);
        var analysis = AiContentValidator.Text(payload.Analysis, MaxAnalysisLength) ?? string.Empty;

        if (analysis.Length == 0 && ideas.Count == 0 && actions.Count == 0)
        {
            logger.LogWarning(
                "AI product recommendations returned no usable content for admin {AdminId}.",
                adminId);

            return ServiceResult<ProductRecommendationReport>.Fail(502, AiMessages.EmptyResponse);
        }

        var report = new ProductRecommendationReport(
            digest.Snapshot,
            analysis,
            digest.Findings,
            ideas,
            actions,
            Merge(digest.Limitations, payload.DataLimitations),
            AiGenerationMeta.Create(completion.Value!));

        return ServiceResult<ProductRecommendationReport>.Ok(report);
    }

    private static IReadOnlyList<ActivityIdea> BuildActivityIdeas(
        IEnumerable<ActivityIdeaPayload>? items)
    {
        var result = new List<ActivityIdea>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var title = AiContentValidator.Text(item.Title, AiContentValidator.TitleMaxLength);
            var description = AiContentValidator.Text(item.Description, MaxIdeaTextLength);
            var whyItFits = AiContentValidator.Text(item.WhyItFits, MaxIdeaTextLength);

            if (title is null
                || description is null
                || whyItFits is null
                || !seen.Add(title))
            {
                continue;
            }

            result.Add(new ActivityIdea(
                AiContentValidator.ActivityKind(item.Kind),
                title,
                description,
                whyItFits,
                AiContentValidator.Effort(item.Effort),
                AiContentValidator.Text(item.ExpectedImpact, MaxIdeaTextLength) ?? string.Empty));

            if (result.Count >= AiContentValidator.MaxActivityIdeas)
            {
                break;
            }
        }

        return result;
    }

    private static IReadOnlyList<RewardIdea> BuildRewardIdeas(
        IEnumerable<RewardIdeaPayload>? items)
    {
        var result = new List<RewardIdea>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var title = AiContentValidator.Text(item.Title, AiContentValidator.TitleMaxLength);
            var description = AiContentValidator.Text(item.Description, MaxIdeaTextLength);
            var rationale = AiContentValidator.Text(item.Rationale, MaxIdeaTextLength);

            if (title is null
                || description is null
                || rationale is null
                || !seen.Add(title))
            {
                continue;
            }

            result.Add(new RewardIdea(
                title,
                description,
                rationale,
                AiContentValidator.Text(item.SuggestedPriceBand, MaxPriceBandLength)));

            if (result.Count >= AiContentValidator.MaxRewardIdeas)
            {
                break;
            }
        }

        return result;
    }

    private static IReadOnlyList<ProductIdea> BuildProductIdeas(
        IEnumerable<ProductIdeaPayload>? items)
    {
        var result = new List<ProductIdea>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var name = AiContentValidator.Text(item.Name, AiContentValidator.TitleMaxLength);
            var description = AiContentValidator.Text(item.Description, AiContentValidator.DescriptionMaxLength);
            var rationale = AiContentValidator.Text(item.Rationale, MaxIdeaTextLength);

            if (name is null
                || description is null
                || rationale is null
                || !seen.Add(name))
            {
                continue;
            }

            var pitch = AiContentValidator.Text(item.ShortPitch, AiContentValidator.ShortPitchMaxLength)
                ?? AiContentValidator.Text(description, AiContentValidator.ShortPitchMaxLength)
                ?? description;

            result.Add(new ProductIdea(
                name,
                AiContentValidator.Text(item.Category, AiContentValidator.TitleMaxLength) ?? "Без категории",
                description,
                pitch,
                AiContentValidator.Text(item.PriceBand, MaxPriceBandLength),
                rationale,
                AiContentValidator.StringList(item.Tags, MaxTags, MaxTagLength)));

            if (result.Count >= AiContentValidator.MaxProductIdeas)
            {
                break;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> Merge(
        IReadOnlyList<string> primary,
        IEnumerable<string?>? secondary)
    {
        var merged = new List<string?>(primary);

        foreach (var item in secondary ?? [])
        {
            merged.Add(item);
        }

        return AiContentValidator.StringList(merged, 10, 400);
    }
}
