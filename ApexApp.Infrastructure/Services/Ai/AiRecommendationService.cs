using System.Globalization;
using System.Text.RegularExpressions;
using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Умные рекомендации. Backend собирает статистику существующими аналитическими
/// сервисами, модель интерпретирует её и выбирает только реальные активности и товары,
/// а решение принимает администратор или мерчандайзер. Результаты не сохраняются.
/// </summary>
public sealed class AiRecommendationService(
    AiStatisticsDigestBuilder digestBuilder,
    AiCompletionExecutor executor,
    IOptions<DeepSeekOptions> options,
    ILogger<AiRecommendationService> logger) : IAiRecommendationService
{
    private const int MaxAnalysisLength = 1500;
    private const int MaxRationaleLength = 800;
    private const int MaxImpactLength = 400;
    private const int MaxActivityRecommendations = 6;
    private const int MaxProductRecommendations = 8;
    private const int MaxLimitations = 10;
    private const int MaxLimitationLength = 400;

    private static readonly Regex NumberPattern = new(
        @"\d+(?:[.,]\d+)?",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ActivityActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "promote",
        "repeat",
        "extend",
        "fix",
        "retire",
        "pause"
    };

    private static readonly HashSet<string> ProductActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "restock",
        "reprice",
        "promote",
        "pause",
        "bundle",
        "replace"
    };

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
            command.Notes,
            MaxActivityRecommendations);

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

        var analysis = AiContentValidator.Text(payload.Analysis, MaxAnalysisLength) ?? string.Empty;
        var hadRecommendations = payload.RecommendedActivities is { Count: > 0 };
        var recommendations = BuildActivityRecommendations(
            payload.RecommendedActivities,
            digest,
            out var dropped);

        if (hadRecommendations && recommendations.Count == 0)
        {
            logger.LogWarning(
                "AI engagement recommendations contained no existing activity for admin {AdminId}.",
                adminId);

            return ServiceResult<EngagementRecommendationReport>.Fail(502, AiMessages.InvalidResponse);
        }

        if (analysis.Length == 0 && recommendations.Count == 0)
        {
            logger.LogWarning(
                "AI engagement recommendations returned no usable content for admin {AdminId}.",
                adminId);

            return ServiceResult<EngagementRecommendationReport>.Fail(502, AiMessages.EmptyResponse);
        }

        var limitations = Merge(digest.Limitations, payload.DataLimitations);

        if (dropped > 0)
        {
            limitations = WithLimitation(
                limitations,
                $"{dropped} рекомендаций отброшено: ссылки отсутствуют в данных.");
        }

        var report = new EngagementRecommendationReport(
            digest.Snapshot,
            digest.Activities,
            analysis,
            digest.Findings,
            recommendations,
            limitations,
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
        var products = digest.TopDemand
            .Concat(digest.StockRisks)
            .Where(row => row.ProductId is not null)
            .GroupBy(row => row.ProductId!.Value)
            .Select(group => group.First())
            .ToList();

        var prompt = AiPromptBuilder.ProductRecommendations(
            digest.Text,
            command.Notes,
            MaxProductRecommendations);

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

        var analysis = AiContentValidator.Text(payload.Analysis, MaxAnalysisLength) ?? string.Empty;
        var hadRecommendations = payload.RecommendedProducts is { Count: > 0 };
        var recommendations = BuildProductRecommendations(
            payload.RecommendedProducts,
            products,
            digest.Findings,
            out var dropped);

        if (hadRecommendations && recommendations.Count == 0)
        {
            logger.LogWarning(
                "AI product recommendations contained no existing product for admin {AdminId}.",
                adminId);

            return ServiceResult<ProductRecommendationReport>.Fail(502, AiMessages.InvalidResponse);
        }

        if (analysis.Length == 0 && recommendations.Count == 0)
        {
            logger.LogWarning(
                "AI product recommendations returned no usable content for admin {AdminId}.",
                adminId);

            return ServiceResult<ProductRecommendationReport>.Fail(502, AiMessages.EmptyResponse);
        }

        var limitations = Merge(digest.Limitations, payload.DataLimitations);

        if (dropped > 0)
        {
            limitations = WithLimitation(
                limitations,
                $"{dropped} рекомендаций отброшено: ссылки отсутствуют в данных.");
        }

        var report = new ProductRecommendationReport(
            digest.Snapshot,
            products,
            analysis,
            digest.Findings,
            recommendations,
            limitations,
            AiGenerationMeta.Create(completion.Value!));

        return ServiceResult<ProductRecommendationReport>.Ok(report);
    }

    private static IReadOnlyList<ActivityRecommendation> BuildActivityRecommendations(
        IEnumerable<ActivityRecommendationPayload>? items,
        AiEngagementDigest digest,
        out int dropped)
    {
        dropped = 0;
        var result = new List<ActivityRecommendation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var activities = digest.Activities
            .GroupBy(row => $"{row.Format.ToLowerInvariant()}:{row.Id}")
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var format = NormalizeFormat(item.Format);
            var activityId = item.ActivityId ?? 0;

            if (format is null
                || activityId <= 0
                || !activities.TryGetValue($"{format}:{activityId}", out var activity))
            {
                dropped++;
                continue;
            }

            var rationale = AiContentValidator.Text(item.Rationale, MaxRationaleLength);
            var expectedImpact = AiContentValidator.Text(item.ExpectedImpact, MaxImpactLength)
                ?? string.Empty;

            if (rationale is null
                || !IsGrounded(rationale, digest.Findings, ActivityMetrics(activity))
                || !seen.Add($"{format}:{activityId}"))
            {
                dropped++;
                continue;
            }

            result.Add(new ActivityRecommendation(
                format,
                activityId,
                activity.Title,
                NormalizeAction(item.Action, ActivityActions, "promote"),
                rationale,
                expectedImpact));

            if (result.Count >= MaxActivityRecommendations)
            {
                break;
            }
        }

        return result;
    }

    private static IReadOnlyList<ProductRecommendation> BuildProductRecommendations(
        IEnumerable<ProductRecommendationPayload>? items,
        IReadOnlyList<AiDemandRow> products,
        IReadOnlyList<AiFinding> findings,
        out int dropped)
    {
        dropped = 0;
        var result = new List<ProductRecommendation>();
        var seen = new HashSet<int>();

        var byId = products
            .Where(row => row.ProductId is not null)
            .GroupBy(row => row.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var productId = item.ProductId ?? 0;

            if (productId <= 0 || !byId.TryGetValue(productId, out var product))
            {
                dropped++;
                continue;
            }

            var rationale = AiContentValidator.Text(item.Rationale, MaxRationaleLength);
            var expectedImpact = AiContentValidator.Text(item.ExpectedImpact, MaxImpactLength)
                ?? string.Empty;

            if (rationale is null
                || !IsGrounded(rationale, findings, ProductMetrics(product))
                || !seen.Add(productId))
            {
                dropped++;
                continue;
            }

            result.Add(new ProductRecommendation(
                productId,
                product.Name,
                NormalizeAction(item.Action, ProductActions, "promote"),
                rationale,
                expectedImpact));

            if (result.Count >= MaxProductRecommendations)
            {
                break;
            }
        }

        return result;
    }

    private static string? NormalizeFormat(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "event" => "event",
            "quest" => "quest",
            "survey" => "survey",
            _ => null
        };

    private static string NormalizeAction(
        string? value,
        HashSet<string> allowed,
        string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized is not null && allowed.Contains(normalized)
            ? normalized
            : fallback;
    }

    private static bool IsGrounded(
        string rationale,
        IReadOnlyList<AiFinding> findings,
        IReadOnlyList<string> metrics)
    {
        if (findings.Any(finding =>
                rationale.Contains(finding.Code, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (Match match in NumberPattern.Matches(rationale))
        {
            var value = match.Value.Replace(',', '.');

            if (metrics.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ActivityMetrics(AiActivityRow activity)
    {
        var metrics = new List<string>
        {
            activity.Participants.ToString(CultureInfo.InvariantCulture),
            activity.Completed.ToString(CultureInfo.InvariantCulture),
            activity.RewardAmount.ToString(CultureInfo.InvariantCulture)
        };

        AddRate(metrics, activity.CompletionRate);

        return metrics;
    }

    private static IReadOnlyList<string> ProductMetrics(AiDemandRow product)
    {
        var metrics = new List<string>
        {
            product.Units.ToString(CultureInfo.InvariantCulture),
            product.UniqueBuyers.ToString(CultureInfo.InvariantCulture),
            product.LiveCartUnits.ToString(CultureInfo.InvariantCulture),
            product.Price.ToString(CultureInfo.InvariantCulture)
        };

        if (product.Stock is int stock)
        {
            metrics.Add(stock.ToString(CultureInfo.InvariantCulture));
        }

        AddRate(metrics, product.SellThrough);

        return metrics;
    }

    private static void AddRate(List<string> metrics, double? value)
    {
        if (value is not double rate)
        {
            return;
        }

        var percent = Math.Round(rate * 100, 1);

        metrics.Add(percent.ToString("0.#", CultureInfo.InvariantCulture));
        metrics.Add(percent.ToString("0", CultureInfo.InvariantCulture) + "%");
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

        return AiContentValidator.StringList(merged, MaxLimitations, MaxLimitationLength);
    }

    private static IReadOnlyList<string> WithLimitation(
        IReadOnlyList<string> limitations,
        string limitation)
    {
        var merged = new List<string?>(limitations)
        {
            limitation
        };

        return AiContentValidator.StringList(merged, MaxLimitations, MaxLimitationLength);
    }
}
