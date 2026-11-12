using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Снимок вовлечённости всех сотрудников на указанный момент.
/// </summary>
/// <param name="WindowStart">Начало расчётного окна (UTC).</param>
/// <param name="WindowEnd">Конец расчётного окна, момент расчёта (UTC).</param>
/// <param name="WindowDays">Длина окна в днях.</param>
/// <param name="ByUser">Рассчитанные статусы по сотрудникам с активностью.</param>
public sealed record EngagementSnapshot(
    DateTime WindowStart,
    DateTime WindowEnd,
    int WindowDays,
    IReadOnlyDictionary<Guid, EngagementSummary> ByUser);

/// <summary>
/// Рассчитывает вовлечённость всех сотрудников за окно, повторно используя
/// <see cref="EngagementCalculator"/> и правила <see cref="EngagementScoring"/>.
/// Второй, конкурирующий расчёт вовлечённости не создаётся.
/// </summary>
public interface IAnalyticsEngagementLoader
{
    /// <summary>
    /// Рассчитывает снимок вовлечённости на момент <paramref name="windowEnd"/>.
    /// </summary>
    /// <param name="windowEnd">Момент расчёта (UTC).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Снимок вовлечённости.</returns>
    Task<EngagementSnapshot> LoadAsync(DateTime windowEnd, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsEngagementLoader(AppDbContext dbContext) : IAnalyticsEngagementLoader
{
    /// <inheritdoc />
    public async Task<EngagementSnapshot> LoadAsync(
        DateTime windowEnd,
        CancellationToken cancellationToken = default)
    {
        // The window boundaries mirror EngagementService exactly so the aggregate
        // numbers can never disagree with the per-user engagement endpoint.
        var windowStart = windowEnd.AddDays(-EngagementScoring.WindowDays);
        var recentStart = windowEnd.AddDays(-EngagementScoring.BandDays);
        var steadyStart = windowEnd.AddDays(-2 * EngagementScoring.BandDays);

        var events = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.Status == EventParticipantStatuses.Attended
                && participant.AttendedAt >= windowStart
                && participant.AttendedAt < windowEnd)
            .GroupBy(participant => participant.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                Weighted = group.Sum(participant => participant.AttendedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : participant.AttendedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                LastAt = group.Max(participant => participant.AttendedAt)
            })
            .ToListAsync(cancellationToken);

        var quests = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.Status == QuestParticipationStatuses.Completed
                && participation.CompletedAt >= windowStart
                && participation.CompletedAt < windowEnd)
            .GroupBy(participation => participation.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                Weighted = group.Sum(participation => participation.CompletedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : participation.CompletedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                LastAt = group.Max(participation => participation.CompletedAt)
            })
            .ToListAsync(cancellationToken);

        var questSteps = await dbContext.QuestStepCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.CompletedAt >= windowStart
                && completion.CompletedAt < windowEnd
                && completion.QuestParticipation.Status != QuestParticipationStatuses.Cancelled
                && (!completion.QuestProofs.Any()
                    || completion.QuestProofs.Any(proof => proof.Status == QuestProofStatuses.Approved)))
            .GroupBy(completion => completion.QuestParticipation.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                Weighted = group.Sum(completion => completion.CompletedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : completion.CompletedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                LastAt = group.Max(completion => completion.CompletedAt)
            })
            .ToListAsync(cancellationToken);

        var thanksSent = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank =>
                thank.FromUserId != thank.ToUserId
                && thank.CreatedAt >= windowStart
                && thank.CreatedAt < windowEnd)
            .GroupBy(thank => thank.FromUserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                Weighted = group.Sum(thank => thank.CreatedAt >= recentStart
                    ? EngagementScoring.RecentWeight
                    : thank.CreatedAt >= steadyStart
                        ? EngagementScoring.SteadyWeight
                        : EngagementScoring.AgedWeight),
                LastAt = group.Max(thank => thank.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var surveyPairs = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SubmittedAt >= windowStart && response.SubmittedAt < windowEnd)
            .GroupBy(response => new { response.UserId, response.SurveyId })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.SurveyId,
                At = group.Max(response => response.SubmittedAt)
            })
            .ToListAsync(cancellationToken);

        var thanksPairs = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank =>
                thank.FromUserId != thank.ToUserId
                && thank.CreatedAt >= windowStart
                && thank.CreatedAt < windowEnd)
            .GroupBy(thank => new { thank.ToUserId, thank.FromUserId })
            .Select(group => new
            {
                group.Key.ToUserId,
                group.Key.FromUserId,
                At = group.Max(thank => thank.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var eventsByUser = events.ToDictionary(
            row => row.UserId,
            row => new EngagementSourceMetrics(row.Count, row.Weighted, row.LastAt));

        var questsByUser = quests.ToDictionary(
            row => row.UserId,
            row => new EngagementSourceMetrics(row.Count, row.Weighted, row.LastAt));

        var stepsByUser = questSteps.ToDictionary(
            row => row.UserId,
            row => new EngagementSourceMetrics(row.Count, row.Weighted, row.LastAt));

        var thanksSentByUser = thanksSent.ToDictionary(
            row => row.UserId,
            row => new EngagementSourceMetrics(row.Count, row.Weighted, row.LastAt));

        var surveyMoments = surveyPairs
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DateTime>)group.Select(row => row.At).ToList());

        var thanksReceivedMoments = thanksPairs
            .GroupBy(row => row.ToUserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DateTime>)group.Select(row => row.At).ToList());

        var users = new HashSet<Guid>();

        users.UnionWith(eventsByUser.Keys);
        users.UnionWith(questsByUser.Keys);
        users.UnionWith(stepsByUser.Keys);
        users.UnionWith(thanksSentByUser.Keys);
        users.UnionWith(surveyMoments.Keys);
        users.UnionWith(thanksReceivedMoments.Keys);

        var summaries = new Dictionary<Guid, EngagementSummary>(users.Count);

        foreach (var userId in users)
        {
            var metrics = new EngagementMetrics(
                eventsByUser.GetValueOrDefault(userId, EngagementSourceMetrics.Empty),
                questsByUser.GetValueOrDefault(userId, EngagementSourceMetrics.Empty),
                stepsByUser.GetValueOrDefault(userId, EngagementSourceMetrics.Empty),
                thanksSentByUser.GetValueOrDefault(userId, EngagementSourceMetrics.Empty),
                surveyMoments.GetValueOrDefault(userId, Array.Empty<DateTime>()),
                thanksReceivedMoments.GetValueOrDefault(userId, Array.Empty<DateTime>()));

            summaries[userId] = EngagementCalculator.Evaluate(metrics, windowEnd);
        }

        return new EngagementSnapshot(
            windowStart,
            windowEnd,
            EngagementScoring.WindowDays,
            summaries);
    }
}
