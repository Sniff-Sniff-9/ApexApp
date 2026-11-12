using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Строит агрегаты активности на уровне сотрудника за текущий и предыдущий периоды.
/// Это единственный источник данных для обзора платформы и сравнения отделов, поэтому
/// показатели не дублируются между отчётами.
/// </summary>
public interface IAnalyticsActivityRollupService
{
    /// <summary>
    /// Возвращает агрегаты по сотрудникам, у которых была активность в текущем
    /// или предыдущем периоде.
    /// </summary>
    /// <param name="range">Окно расчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Строки активности по сотрудникам.</returns>
    Task<IReadOnlyList<UserActivityRow>> GetAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsActivityRollupService(
    AppDbContext dbContext,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IAnalyticsActivityRollupService
{
    /// <summary>Типы транзакций, которые считаются начислением баллов за активность.</summary>
    private static readonly TransactionType[] EarningTypes =
    [
        TransactionType.ActivityReward,
        TransactionType.Thanks,
        TransactionType.AchievementReward,
        TransactionType.ManagerBonus,
        TransactionType.DailyBonus
    ];

    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public Task<IReadOnlyList<UserActivityRow>> GetAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync<IReadOnlyList<UserActivityRow>>(
            CacheKeys.AnalyticsActivityRollup(range.CacheTag),
            _options.ActivityRollupTtl,
            token => LoadAsync(range, token),
            cancellationToken);

    private async Task<IReadOnlyList<UserActivityRow>> LoadAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var population = await populationService.GetAsync(cancellationToken);
        var rows = new Dictionary<Guid, MutableRow>();

        MutableRow Row(Guid userId)
        {
            if (!rows.TryGetValue(userId, out var row))
            {
                row = new MutableRow(userId);
                rows[userId] = row;
            }

            return row;
        }

        var from = range.From;
        var to = range.To;
        var previousFrom = range.PreviousFrom;

        var events = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant =>
                participant.Status == EventParticipantStatuses.Attended
                && participant.AttendedAt >= previousFrom
                && participant.AttendedAt < to)
            .GroupBy(participant => new { participant.UserId, IsCurrent = participant.AttendedAt >= from })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Count = group.Count(),
                LastAt = group.Max(participant => participant.AttendedAt)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in events)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);
            segment.EventsAttended += row.Count;
            segment.Touch(row.LastAt);
        }

        var participations = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.StartedAt >= previousFrom
                && participation.StartedAt < to)
            .GroupBy(participation => new
            {
                participation.UserId,
                IsCurrent = participation.StartedAt >= from,
                participation.Status
            })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                group.Key.Status,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in participations)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);

            if (row.Status == QuestParticipationStatuses.Cancelled)
            {
                segment.QuestsJoined += row.Count;
                segment.QuestsCancelled += row.Count;
            }
            else
            {
                segment.QuestsJoined += row.Count;
            }
        }

        var completions = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation =>
                participation.Status == QuestParticipationStatuses.Completed
                && participation.CompletedAt >= previousFrom
                && participation.CompletedAt < to)
            .GroupBy(participation => new
            {
                participation.UserId,
                IsCurrent = participation.CompletedAt >= from
            })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Count = group.Count(),
                LastAt = group.Max(participation => participation.CompletedAt)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in completions)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);
            segment.QuestsCompleted += row.Count;
            segment.Touch(row.LastAt);
        }

        var surveys = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SubmittedAt >= previousFrom && response.SubmittedAt < to)
            .GroupBy(response => new
            {
                response.UserId,
                IsCurrent = response.SubmittedAt >= from
            })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Count = group.Count(),
                DistinctSurveys = group.Select(response => response.SurveyId).Distinct().Count(),
                LastAt = group.Max(response => response.SubmittedAt)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in surveys)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);
            segment.SurveysSubmitted += row.Count;
            segment.DistinctSurveys += row.DistinctSurveys;
            segment.Touch(row.LastAt);
        }

        var thanksSent = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank => thank.FromUserId != thank.ToUserId
                            && thank.CreatedAt >= previousFrom
                            && thank.CreatedAt < to)
            .GroupBy(thank => new { thank.FromUserId, IsCurrent = thank.CreatedAt >= from })
            .Select(group => new
            {
                UserId = group.Key.FromUserId,
                group.Key.IsCurrent,
                Count = group.Count(),
                LastAt = group.Max(thank => thank.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in thanksSent)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);
            segment.ThanksSent += row.Count;
            segment.Touch(row.LastAt);
        }

        var thanksReceived = await dbContext.Thanks
            .AsNoTracking()
            .Where(thank => thank.FromUserId != thank.ToUserId
                            && thank.CreatedAt >= previousFrom
                            && thank.CreatedAt < to)
            .GroupBy(thank => new { thank.ToUserId, IsCurrent = thank.CreatedAt >= from })
            .Select(group => new
            {
                UserId = group.Key.ToUserId,
                group.Key.IsCurrent,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in thanksReceived)
        {
            Row(row.UserId).Segment(row.IsCurrent).ThanksReceived += row.Count;
        }

        var earnings = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                EarningTypes.Contains(transaction.Type)
                && transaction.CreatedAt >= previousFrom
                && transaction.CreatedAt < to)
            .GroupBy(transaction => new
            {
                transaction.UserId,
                IsCurrent = transaction.CreatedAt >= from
            })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Points = group.Sum(transaction => (long?)transaction.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        foreach (var row in earnings)
        {
            Row(row.UserId).Segment(row.IsCurrent).PointsEarned += row.Points;
        }

        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status != OrderStatus.Cancelled
                && order.Status != OrderStatus.Refunded
                && order.CreatedAt >= previousFrom
                && order.CreatedAt < to)
            .GroupBy(order => new { order.UserId, IsCurrent = order.CreatedAt >= from })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Count = group.Count(),
                Points = group.Sum(order => (long?)order.TotalAmount) ?? 0
            })
            .ToListAsync(cancellationToken);

        foreach (var row in orders)
        {
            var segment = Row(row.UserId).Segment(row.IsCurrent);
            segment.Orders += row.Count;
            segment.PointsSpent += row.Points;
        }

        var units = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status != OrderStatus.Cancelled
                && item.Order.Status != OrderStatus.Refunded
                && item.Order.CreatedAt >= previousFrom
                && item.Order.CreatedAt < to)
            .GroupBy(item => new { item.Order.UserId, IsCurrent = item.Order.CreatedAt >= from })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.IsCurrent,
                Units = group.Sum(item => (long)item.Quantity)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in units)
        {
            Row(row.UserId).Segment(row.IsCurrent).UnitsOrdered += row.Units;
        }

        return rows.Values
            .Select(row => new UserActivityRow(
                row.UserId,
                population.DepartmentIdByUser.GetValueOrDefault(row.UserId),
                row.Current.ToImmutable(),
                row.Previous.ToImmutable()))
            .OrderBy(row => row.DepartmentId)
            .ThenBy(row => row.UserId)
            .ToList();
    }

    private sealed class MutableRow(Guid userId)
    {
        public Guid UserId { get; } = userId;

        public MutableSegment Current { get; } = new();

        public MutableSegment Previous { get; } = new();

        public MutableSegment Segment(bool isCurrent) => isCurrent ? Current : Previous;
    }

    private sealed class MutableSegment
    {
        public int EventsAttended { get; set; }

        public int QuestsJoined { get; set; }

        public int QuestsCompleted { get; set; }

        public int QuestsCancelled { get; set; }

        public int SurveysSubmitted { get; set; }

        public int DistinctSurveys { get; set; }

        public int ThanksSent { get; set; }

        public int ThanksReceived { get; set; }

        public int Orders { get; set; }

        public long UnitsOrdered { get; set; }

        public long PointsEarned { get; set; }

        public long PointsSpent { get; set; }

        public DateTime? LastActionAt { get; set; }

        public void Touch(DateTime? moment)
        {
            if (moment is DateTime value && (LastActionAt is null || value > LastActionAt.Value))
            {
                LastActionAt = value;
            }
        }

        public UserActivitySegment ToImmutable() => new(
            EventsAttended,
            QuestsJoined,
            QuestsCompleted,
            QuestsCancelled,
            SurveysSubmitted,
            DistinctSurveys,
            ThanksSent,
            ThanksReceived,
            Orders,
            UnitsOrdered,
            PointsEarned,
            PointsSpent,
            LastActionAt);
    }
}
