using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Рассчитывает вовлечённость пользователя агрегатами на стороне PostgreSQL
/// и кеширует результат на короткое время.
/// </summary>
public sealed class EngagementService(
    AppDbContext dbContext,
    ICacheService cache) : IEngagementService
{
    /// <inheritdoc />
    public async Task<ServiceResult<EngagementSummary>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<EngagementSummary>.Fail(400, "A valid user ID is required.");
        }

        var cacheKey = CacheKeys.Engagement(userId);

        if (cache.TryGet<EngagementSummary>(cacheKey, out var cached) && cached is not null)
        {
            return ServiceResult<EngagementSummary>.Ok(cached);
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);

        if (!userExists)
        {
            return ServiceResult<EngagementSummary>.Fail(404, "User profile was not found.");
        }

        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-EngagementScoring.WindowDays);
        var recentStart = now.AddDays(-EngagementScoring.BandDays);
        var steadyStart = now.AddDays(-2 * EngagementScoring.BandDays);

        var events = await LoadEventsAsync(userId, windowStart, recentStart, steadyStart, cancellationToken);
        var quests = await LoadQuestsAsync(userId, windowStart, recentStart, steadyStart, cancellationToken);
        var questSteps = await LoadQuestStepsAsync(userId, windowStart, recentStart, steadyStart, cancellationToken);
        var thanksSent = await LoadThanksSentAsync(userId, windowStart, recentStart, steadyStart, cancellationToken);

        var surveyMoments = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.UserId == userId && response.SubmittedAt >= windowStart)
            .GroupBy(response => response.SurveyId)
            .Select(group => group.Max(response => response.SubmittedAt))
            .ToListAsync(cancellationToken);

        var thanksReceivedMoments = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank =>
                thank.ToUserId == userId &&
                thank.FromUserId != userId &&
                thank.CreatedAt >= windowStart)
            .GroupBy(thank => thank.FromUserId)
            .Select(group => group.Max(thank => thank.CreatedAt))
            .ToListAsync(cancellationToken);

        var summary = EngagementCalculator.Evaluate(
            new EngagementMetrics(
                events,
                quests,
                questSteps,
                thanksSent,
                surveyMoments,
                thanksReceivedMoments),
            now);

        cache.Set(cacheKey, summary, CacheKeys.EngagementTtl);

        return ServiceResult<EngagementSummary>.Ok(summary);
    }

    private async Task<EngagementSourceMetrics> LoadEventsAsync(
        Guid userId,
        DateTime windowStart,
        DateTime recentStart,
        DateTime steadyStart,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.UserId == userId &&
                participant.Status == EventParticipantStatuses.Attended &&
                participant.AttendedAt >= windowStart)
            .GroupBy(_ => 1)
            .Select(group => new EngagementSourceMetrics(
                group.Count(),
                group.Sum(participant => participant.AttendedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : participant.AttendedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                group.Max(participant => participant.AttendedAt)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? EngagementSourceMetrics.Empty;
    }

    private async Task<EngagementSourceMetrics> LoadQuestsAsync(
        Guid userId,
        DateTime windowStart,
        DateTime recentStart,
        DateTime steadyStart,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.UserId == userId &&
                participation.Status == QuestParticipationStatuses.Completed &&
                participation.CompletedAt >= windowStart)
            .GroupBy(_ => 1)
            .Select(group => new EngagementSourceMetrics(
                group.Count(),
                group.Sum(participation => participation.CompletedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : participation.CompletedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                group.Max(participation => participation.CompletedAt)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? EngagementSourceMetrics.Empty;
    }

    private async Task<EngagementSourceMetrics> LoadQuestStepsAsync(
        Guid userId,
        DateTime windowStart,
        DateTime recentStart,
        DateTime steadyStart,
        CancellationToken cancellationToken)
    {
        // Шаг учитывается, если у него нет проверяемого доказательства (ручной шаг)
        // либо хотя бы одно доказательство подтверждено модератором.
        return await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.UserId == userId &&
                participation.Status != QuestParticipationStatuses.Cancelled)
            .SelectMany(participation => participation.QuestStepCompletions)
            .Where(completion =>
                completion.CompletedAt >= windowStart &&
                (!completion.QuestProofs.Any() ||
                 completion.QuestProofs.Any(proof => proof.Status == QuestProofStatuses.Approved)))
            .GroupBy(_ => 1)
            .Select(group => new EngagementSourceMetrics(
                group.Count(),
                group.Sum(completion => completion.CompletedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : completion.CompletedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                group.Max(completion => completion.CompletedAt)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? EngagementSourceMetrics.Empty;
    }

    private async Task<EngagementSourceMetrics> LoadThanksSentAsync(
        Guid userId,
        DateTime windowStart,
        DateTime recentStart,
        DateTime steadyStart,
        CancellationToken cancellationToken)
    {
        return await dbContext.Thanks
            .AsNoTracking()
            .Where(thank =>
                thank.FromUserId == userId &&
                thank.ToUserId != userId &&
                thank.CreatedAt >= windowStart)
            .GroupBy(_ => 1)
            .Select(group => new EngagementSourceMetrics(
                group.Count(),
                group.Sum(thank => thank.CreatedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : thank.CreatedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                group.Max(thank => thank.CreatedAt)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? EngagementSourceMetrics.Empty;
    }
}
