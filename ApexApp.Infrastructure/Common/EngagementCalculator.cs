using ApexApp.Infrastructure.Services;

namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Агрегированные показатели одного источника активности за расчётное окно.
/// </summary>
/// <param name="Count">Количество учтённых действий (для опросов и полученных благодарностей — количество уникальных).</param>
/// <param name="WeightedCount">Сумма весов действий с учётом их свежести.</param>
/// <param name="LastAt">Момент последнего учтённого действия; <c>null</c>, если действий нет.</param>
public sealed record EngagementSourceMetrics(int Count, double WeightedCount, DateTime? LastAt)
{
    /// <summary>Пустой результат: учтённых действий нет.</summary>
    public static EngagementSourceMetrics Empty { get; } = new(0, 0d, null);
}

/// <summary>
/// Исходные данные для расчёта вовлечённости: только действия внутри расчётного окна.
/// </summary>
/// <param name="Events">Посещённые мероприятия.</param>
/// <param name="Quests">Завершённые задания.</param>
/// <param name="QuestSteps">Подтверждённые шаги заданий.</param>
/// <param name="ThanksSent">Отправленные благодарности.</param>
/// <param name="SurveyMoments">Моменты пройденных опросов (по одному на опрос).</param>
/// <param name="ThanksReceivedMoments">Моменты полученных благодарностей (по одной на коллегу).</param>
public sealed record EngagementMetrics(
    EngagementSourceMetrics Events,
    EngagementSourceMetrics Quests,
    EngagementSourceMetrics QuestSteps,
    EngagementSourceMetrics ThanksSent,
    IReadOnlyList<DateTime> SurveyMoments,
    IReadOnlyList<DateTime> ThanksReceivedMoments);

/// <summary>
/// Чистый расчёт баллов и уровня вовлечённости без обращения к базе данных.
/// </summary>
public static class EngagementCalculator
{
    /// <summary>
    /// Рассчитывает статус вовлечённости по агрегированным показателям.
    /// </summary>
    /// <remarks>
    /// Формула: <c>score = min(100, min(events + quests, 45) + steps + surveys
    /// + thanksReceived + thanksSent + variety)</c>, где каждый компонент предварительно
    /// ограничен своим лимитом, а баллы за действия взвешены по свежести.
    /// </remarks>
    /// <param name="metrics">Агрегированные показатели источников активности.</param>
    /// <param name="now">Момент расчёта (UTC).</param>
    /// <returns>Рассчитанный статус вовлечённости.</returns>
    public static EngagementSummary Evaluate(EngagementMetrics metrics, DateTime now)
    {
        var surveyMoments = metrics.SurveyMoments;
        var thanksReceivedMoments = metrics.ThanksReceivedMoments;

        var surveyLastAt = surveyMoments.Count > 0 ? surveyMoments.Max() : (DateTime?)null;
        var thanksReceivedLastAt = thanksReceivedMoments.Count > 0 ? thanksReceivedMoments.Max() : (DateTime?)null;

        var surveyWeighted = surveyMoments.Sum(moment => EngagementScoring.WeightFor(moment, now));
        var thanksReceivedWeighted = thanksReceivedMoments.Sum(moment => EngagementScoring.WeightFor(moment, now));

        var eventPoints = Cap(
            metrics.Events.WeightedCount * EngagementScoring.EventAttendedPoints,
            EngagementScoring.EventAttendedCap);
        var questPoints = Cap(
            metrics.Quests.WeightedCount * EngagementScoring.QuestCompletedPoints,
            EngagementScoring.QuestCompletedCap);
        var stepPoints = Cap(
            metrics.QuestSteps.WeightedCount * EngagementScoring.QuestStepPoints,
            EngagementScoring.QuestStepCap);
        var surveyPoints = Cap(surveyWeighted * EngagementScoring.SurveyPoints, EngagementScoring.SurveyCap);
        var thanksReceivedPoints = Cap(
            thanksReceivedWeighted * EngagementScoring.ThanksReceivedPoints,
            EngagementScoring.ThanksReceivedCap);
        var thanksSentPoints = Cap(
            metrics.ThanksSent.WeightedCount * EngagementScoring.ThanksSentPoints,
            EngagementScoring.ThanksSentCap);

        var deepPoints = Cap(eventPoints + questPoints, EngagementScoring.DeepActivityCap);

        var varietyCutoff = now.AddDays(-EngagementScoring.VarietyRecencyDays);
        DateTime?[] sourceMoments =
        [
            metrics.Events.LastAt,
            metrics.Quests.LastAt,
            metrics.QuestSteps.LastAt,
            surveyLastAt,
            thanksReceivedLastAt,
            metrics.ThanksSent.LastAt
        ];

        var activeSources = sourceMoments.Count(moment => moment is DateTime value && value >= varietyCutoff);
        var varietyPoints = Cap(activeSources * EngagementScoring.VarietyPoints, EngagementScoring.VarietyCap);

        var rawScore = deepPoints + stepPoints + surveyPoints + thanksReceivedPoints + thanksSentPoints + varietyPoints;
        var score = EngagementScoring.Round(Math.Clamp(rawScore, 0d, EngagementScoring.MaxScore));

        var tier = EngagementScoring.ResolveTier(score);
        var nextTier = EngagementScoring.NextTier(score);

        var progress = 1d;
        if (nextTier is not null)
        {
            var span = nextTier.MinScore - tier.MinScore;
            progress = span <= 0d ? 1d : Math.Clamp((score - tier.MinScore) / span, 0d, 1d);
        }

        var lastActivityAt = sourceMoments.Max();

        var breakdown = new List<EngagementBreakdown>
        {
            new("events", "Посещенные мероприятия", metrics.Events.Count,
                EngagementScoring.Round(eventPoints), EngagementScoring.EventAttendedCap),
            new("quests", "Завершенные задания", metrics.Quests.Count,
                EngagementScoring.Round(questPoints), EngagementScoring.QuestCompletedCap),
            new("quest_steps", "Шаги заданий", metrics.QuestSteps.Count,
                EngagementScoring.Round(stepPoints), EngagementScoring.QuestStepCap),
            new("surveys", "Пройденные опросы", surveyMoments.Count,
                EngagementScoring.Round(surveyPoints), EngagementScoring.SurveyCap),
            new("thanks_received", "Полученные благодарности", thanksReceivedMoments.Count,
                EngagementScoring.Round(thanksReceivedPoints), EngagementScoring.ThanksReceivedCap),
            new("thanks_sent", "Отправленные благодарности", metrics.ThanksSent.Count,
                EngagementScoring.Round(thanksSentPoints), EngagementScoring.ThanksSentCap),
            new("variety", "Разнообразие активности", activeSources,
                EngagementScoring.Round(varietyPoints), EngagementScoring.VarietyCap)
        };

        return new EngagementSummary(
            Status: tier.Code,
            StatusTitle: tier.Title,
            StatusRank: tier.Rank,
            Score: score,
            MaxScore: EngagementScoring.MaxScore,
            NextStatus: nextTier?.Code,
            NextStatusTitle: nextTier?.Title,
            NextStatusScore: nextTier?.MinScore,
            PointsToNext: nextTier is null
                ? null
                : EngagementScoring.Round(Math.Max(nextTier.MinScore - score, 0d)),
            NextLevelProgress: Math.Round(progress, 3),
            WindowDays: EngagementScoring.WindowDays,
            WindowStart: now.AddDays(-EngagementScoring.WindowDays),
            WindowEnd: now,
            LastActivityAt: lastActivityAt,
            Breakdown: breakdown,
            ComputedAt: now);
    }

    private static double Cap(double points, double maxPoints) => Math.Min(points, maxPoints);
}
