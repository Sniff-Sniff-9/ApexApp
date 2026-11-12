using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <inheritdoc />
public sealed class ActivityAnalyticsService(
    AppDbContext dbContext,
    IAnalyticsRangeFactory rangeFactory,
    IAnalyticsPopulationService populationService,
    AnalyticsCache cache,
    IOptions<AnalyticsOptions> options) : IActivityAnalyticsService
{
    private const int MaxActivitiesPerFormat = 1000;

    private readonly AnalyticsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ServiceResult<ActivityAnalyticsResult>> GetActivitiesAsync(
        AnalyticsQuery query,
        string? format,
        ActivitySortBy sortBy,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeFormat(format, out var normalizedFormat))
        {
            return ServiceResult<ActivityAnalyticsResult>.Fail(
                400,
                "Format must be one of: event, quest, survey.");
        }

        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<ActivityAnalyticsResult>.Fail(
                rangeResult.StatusCode,
                rangeResult.Error!);
        }

        var range = rangeResult.Value!;
        var cacheKey = CacheKeys.Analytics(
            $"activities-{sortBy}",
            range.CacheTag,
            AnalyticsCacheTags.Filter(query.DepartmentId) + "-" + (normalizedFormat ?? "all"),
            AnalyticsCacheTags.InstantBucket(range.AsOf));

        var result = await cache.GetOrCreateAsync(
            cacheKey,
            _options.OverviewTtl,
            token => BuildListAsync(range, normalizedFormat, sortBy, query.Top, token),
            cancellationToken);

        return ServiceResult<ActivityAnalyticsResult>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ActivityDetail>> GetDetailAsync(
        AnalyticsQuery query,
        string format,
        long id,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeFormat(format, out var normalizedFormat) || normalizedFormat is null)
        {
            return ServiceResult<ActivityDetail>.Fail(
                400,
                "Format must be one of: event, quest, survey.");
        }

        if (id <= 0)
        {
            return ServiceResult<ActivityDetail>.Fail(400, "A valid activity ID is required.");
        }

        var rangeResult = rangeFactory.Create(query);

        if (!rangeResult.IsSuccess)
        {
            return ServiceResult<ActivityDetail>.Fail(rangeResult.StatusCode, rangeResult.Error!);
        }

        var range = rangeResult.Value!;

        var cacheKey = CacheKeys.Analytics(
            $"activity-detail-{normalizedFormat}-{id}",
            range.CacheTag,
            AnalyticsCacheTags.Filter(query.DepartmentId),
            AnalyticsCacheTags.InstantBucket(range.AsOf));

        // Кеш хранит только ссылочные типы, поэтому отсутствие активности кешируется
        // внутри контейнера: повторные запросы не ходят в базу.
        var box = await cache.GetOrCreateAsync(
            cacheKey,
            _options.OverviewTtl,
            async token => new DetailBox(await BuildDetailAsync(range, normalizedFormat, id, token)),
            cancellationToken);

        var detail = box.Value;

        return detail is null
            ? ServiceResult<ActivityDetail>.Fail(404, "Activity was not found.")
            : ServiceResult<ActivityDetail>.Ok(detail);
    }

    private async Task<ActivityAnalyticsResult> BuildListAsync(
        AnalyticsRange range,
        string? format,
        ActivitySortBy sortBy,
        int top,
        CancellationToken cancellationToken)
    {
        var headcount = (await populationService.GetAsync(cancellationToken)).ActiveUsers;
        var items = new List<ActivityRow>();

        if (format is null or "event")
        {
            items.AddRange(await LoadEventRowsAsync(range, headcount, cancellationToken));
        }

        if (format is null or "quest")
        {
            items.AddRange(await LoadQuestRowsAsync(range, cancellationToken));
        }

        if (format is null or "survey")
        {
            items.AddRange(await LoadSurveyRowsAsync(range, headcount, cancellationToken));
        }

        var ordered = sortBy switch
        {
            ActivitySortBy.CompletionRate => items
                .OrderByDescending(item => item.CompletionRate ?? -1d)
                .ThenByDescending(item => item.Participants),
            ActivitySortBy.RewardPoints => items
                .OrderByDescending(item => item.PointsAwarded)
                .ThenByDescending(item => item.Participants),
            ActivitySortBy.StartDate => items
                .OrderByDescending(item => item.StartsAt ?? DateTime.MinValue)
                .ThenByDescending(item => item.Id),
            _ => items
                .OrderByDescending(item => item.Participants)
                .ThenByDescending(item => item.Completed)
        };

        var take = Math.Clamp(top, 1, _options.MaxTop);

        return new ActivityAnalyticsResult(
            ordered.Take(take).ToList(),
            items.Count,
            range,
            range.Caveats);
    }

    private async Task<List<ActivityRow>> LoadEventRowsAsync(
        AnalyticsRange range,
        long headcount,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.EndAt >= range.From && eventItem.StartAt < range.To)
            .OrderByDescending(eventItem => eventItem.StartAt)
            .Take(MaxActivitiesPerFormat)
            .Select(eventItem => new
            {
                eventItem.Id,
                eventItem.Title,
                eventItem.IsActive,
                eventItem.StartAt,
                eventItem.EndAt,
                eventItem.RewardAmount
            })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return [];
        }

        var ids = events.Select(eventItem => eventItem.Id).ToList();

        var statusRows = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => ids.Contains(participant.EventId))
            .GroupBy(participant => new { participant.EventId, participant.Status })
            .Select(group => new
            {
                group.Key.EventId,
                group.Key.Status,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var statusByEvent = statusRows
            .GroupBy(row => row.EventId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal));

        var claimedRows = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => ids.Contains(participant.EventId) && participant.RewardClaimed)
            .GroupBy(participant => participant.EventId)
            .Select(group => new { EventId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var claimedByEvent = claimedRows.ToDictionary(row => row.EventId, row => row.Count);
        var points = await LoadActivityRewardsAsync("event", ids, range, cancellationToken);

        var rows = new List<ActivityRow>(events.Count);

        foreach (var eventItem in events)
        {
            var byStatus = statusByEvent.GetValueOrDefault(eventItem.Id)
                           ?? new Dictionary<string, int>(StringComparer.Ordinal);

            var registered = byStatus.GetValueOrDefault(EventParticipantStatuses.Registered);
            var attended = byStatus.GetValueOrDefault(EventParticipantStatuses.Attended);
            var noShow = byStatus.GetValueOrDefault(EventParticipantStatuses.NoShow);
            var participants = registered + attended + noShow;
            var showDenominator = attended + noShow;
            var claimed = claimedByEvent.GetValueOrDefault(eventItem.Id);

            rows.Add(new ActivityRow(
                "event",
                eventItem.Id,
                eventItem.Title,
                eventItem.IsActive,
                eventItem.StartAt,
                eventItem.EndAt,
                eventItem.RewardAmount,
                participants,
                attended,
                showDenominator >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(attended, showDenominator), 3)
                    : null,
                registered,
                attended,
                noShow,
                null,
                null,
                attended > claimed ? attended - claimed : 0,
                points.GetValueOrDefault(eventItem.Id)));
        }

        return rows;
    }

    private async Task<List<ActivityRow>> LoadQuestRowsAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var quests = await dbContext.Quests
            .AsNoTracking()
            .Where(quest => quest.CreatedAt < range.To
                            && (quest.EndAt == null || quest.EndAt >= range.From))
            .OrderByDescending(quest => quest.CreatedAt)
            .Take(MaxActivitiesPerFormat)
            .Select(quest => new
            {
                quest.Id,
                quest.Title,
                quest.IsActive,
                quest.StartAt,
                quest.EndAt,
                quest.RewardAmount,
                quest.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (quests.Count == 0)
        {
            return [];
        }

        var ids = quests.Select(quest => quest.Id).ToList();

        var statusRows = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation => ids.Contains(participation.QuestId))
            .GroupBy(participation => new { participation.QuestId, participation.Status })
            .Select(group => new
            {
                group.Key.QuestId,
                group.Key.Status,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var statusByQuest = statusRows
            .GroupBy(row => row.QuestId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal));

        var claimedRows = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation => ids.Contains(participation.QuestId) && participation.RewardClaimed)
            .GroupBy(participation => participation.QuestId)
            .Select(group => new { QuestId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var claimedByQuest = claimedRows.ToDictionary(row => row.QuestId, row => row.Count);

        var pendingRows = await dbContext.QuestProofs
            .AsNoTracking()
            .Where(proof => proof.Status == QuestProofStatuses.Pending
                            && ids.Contains(proof.Completion.QuestId))
            .GroupBy(proof => proof.Completion.QuestId)
            .Select(group => new { QuestId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var pendingByQuest = pendingRows.ToDictionary(row => row.QuestId, row => row.Count);
        var points = await LoadActivityRewardsAsync("quest", ids, range, cancellationToken);

        var rows = new List<ActivityRow>(quests.Count);

        foreach (var quest in quests)
        {
            var byStatus = statusByQuest.GetValueOrDefault(quest.Id)
                           ?? new Dictionary<string, int>(StringComparer.Ordinal);

            var inProgress = byStatus.GetValueOrDefault(QuestParticipationStatuses.InProgress);
            var completed = byStatus.GetValueOrDefault(QuestParticipationStatuses.Completed);
            var cancelled = byStatus.GetValueOrDefault(QuestParticipationStatuses.Cancelled);
            var total = inProgress + completed + cancelled;
            var claimed = claimedByQuest.GetValueOrDefault(quest.Id);

            rows.Add(new ActivityRow(
                "quest",
                quest.Id,
                quest.Title,
                quest.IsActive,
                quest.StartAt ?? quest.CreatedAt,
                quest.EndAt,
                quest.RewardAmount,
                total,
                completed,
                total >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(completed, total), 3)
                    : null,
                null,
                null,
                null,
                null,
                pendingByQuest.GetValueOrDefault(quest.Id),
                completed > claimed ? completed - claimed : 0,
                points.GetValueOrDefault(quest.Id)));
        }

        return rows;
    }

    private async Task<List<ActivityRow>> LoadSurveyRowsAsync(
        AnalyticsRange range,
        long headcount,
        CancellationToken cancellationToken)
    {
        var surveys = await dbContext.Surveys
            .AsNoTracking()
            .Where(survey => survey.CreatedAt < range.To
                             && (survey.EndAt == null || survey.EndAt >= range.From))
            .OrderByDescending(survey => survey.CreatedAt)
            .Take(MaxActivitiesPerFormat)
            .Select(survey => new
            {
                survey.Id,
                survey.Title,
                survey.IsActive,
                survey.StartAt,
                survey.EndAt,
                survey.RewardAmount,
                survey.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (surveys.Count == 0)
        {
            return [];
        }

        var ids = surveys.Select(survey => survey.Id).ToList();

        var responseRows = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => ids.Contains(response.SurveyId))
            .GroupBy(response => response.SurveyId)
            .Select(group => new
            {
                SurveyId = group.Key,
                Count = group.Count(),
                Users = group.Select(response => response.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var bySurvey = responseRows.ToDictionary(row => row.SurveyId);
        var points = await LoadActivityRewardsAsync("survey", ids, range, cancellationToken);

        var rows = new List<ActivityRow>(surveys.Count);

        foreach (var survey in surveys)
        {
            var response = bySurvey.GetValueOrDefault(survey.Id);
            var respondents = response?.Users ?? 0;

            rows.Add(new ActivityRow(
                "survey",
                survey.Id,
                survey.Title,
                survey.IsActive,
                survey.StartAt ?? survey.CreatedAt,
                survey.EndAt,
                survey.RewardAmount,
                respondents,
                response?.Count ?? 0,
                headcount >= _options.MinCohort
                    ? AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(respondents, headcount), 3)
                    : null,
                null,
                null,
                null,
                response?.Count ?? 0,
                null,
                null,
                points.GetValueOrDefault(survey.Id)));
        }

        return rows;
    }

    private async Task<IReadOnlyDictionary<long, long>> LoadActivityRewardsAsync(
        string entityType,
        IReadOnlyCollection<long> ids,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<long, long>();
        }

        var idList = ids.ToList();

        var rows = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Type == TransactionType.ActivityReward
                && transaction.RelatedEntityType == entityType
                && transaction.RelatedEntityId != null
                && idList.Contains(transaction.RelatedEntityId!.Value)
                && transaction.CreatedAt >= range.From
                && transaction.CreatedAt < range.To)
            .GroupBy(transaction => transaction.RelatedEntityId!.Value)
            .Select(group => new
            {
                Id = group.Key,
                Points = group.Sum(transaction => (long?)transaction.Amount) ?? 0
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => row.Points);
    }

    private async Task<ActivityDetail?> BuildDetailAsync(
        AnalyticsRange range,
        string format,
        long id,
        CancellationToken cancellationToken) => format switch
    {
        "event" => await BuildEventDetailAsync(range, id, cancellationToken),
        "quest" => await BuildQuestDetailAsync(range, id, cancellationToken),
        _ => await BuildSurveyDetailAsync(range, id, cancellationToken)
    };

    private async Task<ActivityDetail?> BuildEventDetailAsync(
        AnalyticsRange range,
        long id,
        CancellationToken cancellationToken)
    {
        var eventItem = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (eventItem is null)
        {
            return null;
        }

        var statusRows = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => participant.EventId == id)
            .GroupBy(participant => participant.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = statusRows.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);

        var claimed = await dbContext.EventParticipants
            .AsNoTracking()
            .LongCountAsync(
                participant => participant.EventId == id && participant.RewardClaimed,
                cancellationToken);

        var registered = byStatus.GetValueOrDefault(EventParticipantStatuses.Registered);
        var attended = byStatus.GetValueOrDefault(EventParticipantStatuses.Attended);
        var noShow = byStatus.GetValueOrDefault(EventParticipantStatuses.NoShow);
        var cancelled = byStatus.GetValueOrDefault(EventParticipantStatuses.Cancelled);
        var participants = registered + attended + noShow;

        var series = await LoadDetailSeriesAsync(range, "event", id, cancellationToken);

        var funnel = new List<ActivityFunnelStep>
        {
            new("registered", "Зарегистрировались", participants, null, 1d),
            new(
                "attended",
                "Посетили",
                attended,
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(attended, participants), 3),
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(attended, participants), 3)),
            new(
                "reward",
                "Получили награду",
                claimed,
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(claimed, attended), 3),
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(claimed, participants), 3))
        };

        var metrics = new List<MetricValue>
        {
            MetricValue.Count("participants", "Участники", participants),
            MetricValue.Count("attended", "Посетили", attended),
            MetricValue.Count("no_show", "Не явились", noShow),
            MetricValue.Count("cancelled", "Отменили регистрацию", cancelled),
            MetricValue.Rate("attendance_rate", "Доля посещения", attended, attended + noShow, _options.MinCohort),
            MetricValue.Rate("reward_claim_rate", "Доля полученных наград", claimed, attended, _options.MinCohort)
        };

        var caveats = new List<string>(range.Caveats)
        {
            "Регистрация может быть отменена и восстановлена: момент регистрации перезаписывается, " +
            "поэтому воронка отражает текущее состояние, а не полную историю."
        };

        return new ActivityDetail(
            "event",
            eventItem.Id,
            eventItem.Title,
            eventItem.Description,
            eventItem.IsActive,
            eventItem.StartAt,
            eventItem.EndAt,
            eventItem.RewardAmount,
            metrics,
            funnel,
            [],
            series,
            caveats);
    }

    private async Task<ActivityDetail?> BuildQuestDetailAsync(
        AnalyticsRange range,
        long id,
        CancellationToken cancellationToken)
    {
        var quest = await dbContext.Quests
            .AsNoTracking()
            .Include(item => item.QuestSteps)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (quest is null)
        {
            return null;
        }

        var statusRows = await dbContext.QuestParticipations
            .AsNoTracking()
            .Where(participation => participation.QuestId == id)
            .GroupBy(participation => participation.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = statusRows.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);

        var inProgress = byStatus.GetValueOrDefault(QuestParticipationStatuses.InProgress);
        var completed = byStatus.GetValueOrDefault(QuestParticipationStatuses.Completed);
        var cancelled = byStatus.GetValueOrDefault(QuestParticipationStatuses.Cancelled);
        var total = inProgress + completed + cancelled;

        var claimed = await dbContext.QuestParticipations
            .AsNoTracking()
            .LongCountAsync(
                participation => participation.QuestId == id && participation.RewardClaimed,
                cancellationToken);

        var stepRows = await dbContext.QuestStepCompletions
            .AsNoTracking()
            .Where(completion => completion.QuestId == id)
            .GroupBy(completion => completion.StepId)
            .Select(group => new
            {
                StepId = group.Key,
                Users = group.Select(completion => completion.QuestParticipation.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var reachByStep = stepRows.ToDictionary(row => row.StepId, row => row.Users);

        var steps = quest.QuestSteps
            .OrderBy(step => step.SortOrder)
            .ThenBy(step => step.Id)
            .Select(step => new ActivityStepDropOff(
                step.Id,
                step.Title,
                step.SortOrder,
                step.IsRequired,
                reachByStep.GetValueOrDefault(step.Id),
                total > 0
                    ? AnalyticsMetrics.Round(
                        AnalyticsMetrics.Ratio(reachByStep.GetValueOrDefault(step.Id), total),
                        3)
                    : null))
            .ToList();

        var series = await LoadDetailSeriesAsync(range, "quest", id, cancellationToken);
        var averageHours = await LoadAverageQuestHoursAsync(range, id, cancellationToken);

        var funnel = new List<ActivityFunnelStep>
        {
            new("joined", "Начали задание", total, null, 1d),
            new(
                "completed",
                "Завершили",
                completed,
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(completed, total), 3),
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(completed, total), 3)),
            new(
                "reward",
                "Обработали награду",
                claimed,
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(claimed, completed), 3),
                AnalyticsMetrics.Round(AnalyticsMetrics.Ratio(claimed, total), 3))
        };

        var metrics = new List<MetricValue>
        {
            MetricValue.Count("participants", "Участники", total),
            MetricValue.Count("completed", "Завершили", completed),
            MetricValue.Count("in_progress", "В процессе", inProgress),
            MetricValue.Count("cancelled", "Отменили", cancelled),
            MetricValue.Rate("completion_rate", "Доля завершения", completed, total, _options.MinCohort),
            averageHours is null
                ? MetricValue.Unavailable(
                    "average_duration_hours",
                    "Средняя длительность, ч",
                    "В окне нет завершённых заданий.")
                : new MetricValue(
                    "average_duration_hours",
                    "Средняя длительность, ч",
                    null,
                    null,
                    AnalyticsMetrics.Round(averageHours.Value, 1),
                    "hours",
                    true,
                    null)
        };

        var caveats = new List<string>(range.Caveats)
        {
            "Прохождение шагов учитывает доказательства, загруженные повторно: " +
            "момент выполнения шага может быть обновлён."
        };

        return new ActivityDetail(
            "quest",
            quest.Id,
            quest.Title,
            quest.Description,
            quest.IsActive,
            quest.StartAt,
            quest.EndAt,
            quest.RewardAmount,
            metrics,
            funnel,
            steps,
            series,
            caveats);
    }

    private async Task<ActivityDetail?> BuildSurveyDetailAsync(
        AnalyticsRange range,
        long id,
        CancellationToken cancellationToken)
    {
        var survey = await dbContext.Surveys
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (survey is null)
        {
            return null;
        }

        var population = await populationService.GetAsync(cancellationToken);

        var responses = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SurveyId == id)
            .GroupBy(response => response.SurveyId)
            .Select(group => new
            {
                Count = group.Count(),
                Users = group.Select(response => response.UserId).Distinct().Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var series = await LoadDetailSeriesAsync(range, "survey", id, cancellationToken);
        var respondents = responses?.Users ?? 0;
        var submissions = responses?.Count ?? 0;

        var funnel = new List<ActivityFunnelStep>
        {
            new("responded", "Ответили", respondents, null, 1d)
        };

        var metrics = new List<MetricValue>
        {
            MetricValue.Count("participants", "Респонденты", respondents),
            MetricValue.Count("responses", "Ответы", submissions),
            MetricValue.Rate(
                "response_rate",
                "Доля ответивших от активных сотрудников",
                respondents,
                population.ActiveUsers,
                _options.MinCohort)
        };

        var caveats = new List<string>(range.Caveats)
        {
            "Опрос может допускать несколько ответов одного сотрудника: " +
            "число ответов и число респондентов различаются."
        };

        return new ActivityDetail(
            "survey",
            survey.Id,
            survey.Title,
            survey.Description,
            survey.IsActive,
            survey.StartAt,
            survey.EndAt,
            survey.RewardAmount,
            metrics,
            funnel,
            [],
            series,
            caveats);
    }

    private async Task<IReadOnlyList<AnalyticsSeriesPoint>> LoadDetailSeriesAsync(
        AnalyticsRange range,
        string format,
        long id,
        CancellationToken cancellationToken)
    {
        var buckets = AnalyticsBuckets.Build(range, _options.MaxBuckets);
        var part = range.GranularityCode;

        var rows = format switch
        {
            "event" => await dbContext.Database.SqlQuery<SeriesBucketRow>($"""
                SELECT (date_trunc({part}, ep.attended_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                       COUNT(*)::bigint AS "Value",
                       COUNT(DISTINCT ep.user_id)::bigint AS "Users"
                FROM public.event_participants ep
                WHERE ep.event_id = {id} AND ep.status = 'attended'
                  AND ep.attended_at >= {range.From} AND ep.attended_at < {range.To}
                GROUP BY 1
                ORDER BY 1
                """).ToListAsync(cancellationToken),

            "quest" => await dbContext.Database.SqlQuery<SeriesBucketRow>($"""
                SELECT (date_trunc({part}, qp.completed_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                       COUNT(*)::bigint AS "Value",
                       COUNT(DISTINCT qp.user_id)::bigint AS "Users"
                FROM public.quest_participations qp
                WHERE qp.quest_id = {id} AND qp.status = 'completed'
                  AND qp.completed_at >= {range.From} AND qp.completed_at < {range.To}
                GROUP BY 1
                ORDER BY 1
                """).ToListAsync(cancellationToken),

            _ => await dbContext.Database.SqlQuery<SeriesBucketRow>($"""
                SELECT (date_trunc({part}, sr.submitted_at AT TIME ZONE {range.TimeZone}) AT TIME ZONE {range.TimeZone}) AS "Bucket",
                       COUNT(*)::bigint AS "Value",
                       COUNT(DISTINCT sr.user_id)::bigint AS "Users"
                FROM public.survey_responses sr
                WHERE sr.survey_id = {id}
                  AND sr.submitted_at >= {range.From} AND sr.submitted_at < {range.To}
                GROUP BY 1
                ORDER BY 1
                """).ToListAsync(cancellationToken)
        };

        return buckets
            .Select(bucket =>
            {
                var match = rows.FirstOrDefault(row => row.Bucket == bucket.Start);

                return new AnalyticsSeriesPoint(
                    bucket.Start,
                    bucket.End,
                    match?.Value ?? 0,
                    match?.Users ?? 0,
                    null,
                    null);
            })
            .ToList();
    }

    private async Task<double?> LoadAverageQuestHoursAsync(
        AnalyticsRange range,
        long questId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<ScalarDoubleRow>($"""
            SELECT AVG(EXTRACT(EPOCH FROM (qp.completed_at - qp.started_at)) / 3600.0) AS "Value"
            FROM public.quest_participations qp
            WHERE qp.quest_id = {questId}
              AND qp.status = 'completed'
              AND qp.completed_at >= {range.From}
              AND qp.completed_at < {range.To}
            """).ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : rows[0].Value;
    }

    private static bool TryNormalizeFormat(string? format, out string? normalized)    {
        switch (format?.Trim().ToLowerInvariant())
        {
            case null or "":
                normalized = null;
                return true;
            case "event":
                normalized = "event";
                return true;
            case "quest":
                normalized = "quest";
                return true;
            case "survey":
                normalized = "survey";
                return true;
            default:
                normalized = null;
                return false;
        }
    }

    /// <summary>
    /// Контейнер результата детализации для кеша: позволяет кешировать и отсутствие активности.
    /// </summary>
    /// <param name="Value">Детализация или <c>null</c>, если активности нет.</param>
    private sealed record DetailBox(ActivityDetail? Value);
}
