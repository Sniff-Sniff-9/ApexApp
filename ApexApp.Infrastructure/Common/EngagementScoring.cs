namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Уровень вовлечённости пользователя.
/// </summary>
/// <param name="Code">Стабильный машинный код уровня.</param>
/// <param name="Title">Название уровня для отображения.</param>
/// <param name="Rank">Порядковый номер уровня: 1 — самый низкий, 5 — самый высокий.</param>
/// <param name="MinScore">Минимальный балл, с которого начинается уровень.</param>
public sealed record EngagementTier(string Code, string Title, int Rank, double MinScore);

/// <summary>
/// Единая точка настройки расчёта вовлечённости: окно, веса, ограничения и пороги уровней.
/// Значения не хранятся в БД, поэтому их изменение не требует миграций.
/// </summary>
public static class EngagementScoring
{
    /// <summary>Длина расчётного окна в днях.</summary>
    public const int WindowDays = 30;

    /// <summary>Длина одной полосы «свежести» действия в днях.</summary>
    public const int BandDays = 7;

    /// <summary>Вес действия возрастом 0–7 дней.</summary>
    public const double RecentWeight = 1.0d;

    /// <summary>Вес действия возрастом 8–14 дней.</summary>
    public const double SteadyWeight = 0.7d;

    /// <summary>Вес действия возрастом 15–30 дней.</summary>
    public const double AgedWeight = 0.4d;

    /// <summary>Максимальный итоговый балл.</summary>
    public const double MaxScore = 100d;

    /// <summary>Окно, в котором источник активности считается «разнообразием».</summary>
    public const int VarietyRecencyDays = 14;

    /// <summary>Баллы за одно посещённое мероприятие (с учётом веса свежести).</summary>
    public const double EventAttendedPoints = 15d;

    /// <summary>Ограничение баллов за мероприятия.</summary>
    public const double EventAttendedCap = 30d;

    /// <summary>Баллы за одно завершённое задание (с учётом веса свежести).</summary>
    public const double QuestCompletedPoints = 12d;

    /// <summary>Ограничение баллов за задания.</summary>
    public const double QuestCompletedCap = 36d;

    /// <summary>Совместное ограничение баллов за мероприятия и задания.</summary>
    public const double DeepActivityCap = 45d;

    /// <summary>Баллы за один подтверждённый шаг задания (с учётом веса свежести).</summary>
    public const double QuestStepPoints = 2d;

    /// <summary>Ограничение баллов за шаги заданий.</summary>
    public const double QuestStepCap = 12d;

    /// <summary>Баллы за один пройденный опрос (с учётом веса свежести).</summary>
    public const double SurveyPoints = 6d;

    /// <summary>Ограничение баллов за опросы.</summary>
    public const double SurveyCap = 18d;

    /// <summary>Баллы за одну благодарность от коллеги (с учётом веса свежести).</summary>
    public const double ThanksReceivedPoints = 3d;

    /// <summary>Ограничение баллов за полученные благодарности.</summary>
    public const double ThanksReceivedCap = 12d;

    /// <summary>Баллы за одну отправленную благодарность (с учётом веса свежести).</summary>
    public const double ThanksSentPoints = 2d;

    /// <summary>Ограничение баллов за отправленные благодарности.</summary>
    public const double ThanksSentCap = 6d;

    /// <summary>Баллы за каждый активный источник активности.</summary>
    public const double VarietyPoints = 3d;

    /// <summary>Ограничение баллов за разнообразие активности.</summary>
    public const double VarietyCap = 15d;

    /// <summary>Код уровня «Искра».</summary>
    public const string Spark = "spark";

    /// <summary>Код уровня «Мерцание».</summary>
    public const string Flicker = "flicker";

    /// <summary>Код уровня «Сияние».</summary>
    public const string Radiance = "radiance";

    /// <summary>Код уровня «Сверхсияние».</summary>
    public const string SuperRadiance = "super_radiance";

    /// <summary>Код уровня «Зенит».</summary>
    public const string Zenith = "zenith";

    /// <summary>Уровни вовлечённости по возрастанию.</summary>
    public static readonly IReadOnlyList<EngagementTier> Tiers =
    [
        new(Spark, "Искра", 1, 0d),
        new(Flicker, "Мерцание", 2, 18d),
        new(Radiance, "Сияние", 3, 38d),
        new(SuperRadiance, "Сверхсияние", 4, 60d),
        new(Zenith, "Зенит", 5, 80d)
    ];

    /// <summary>
    /// Возвращает вес действия в зависимости от его возраста.
    /// </summary>
    /// <param name="moment">Момент действия.</param>
    /// <param name="now">Момент расчёта.</param>
    /// <returns>Вес действия: 1.0, 0.7 или 0.4.</returns>
    public static double WeightFor(DateTime moment, DateTime now)
    {
        var ageInDays = (now - moment).TotalDays;

        if (ageInDays <= BandDays)
        {
            return RecentWeight;
        }

        return ageInDays <= 2 * BandDays ? SteadyWeight : AgedWeight;
    }

    /// <summary>
    /// Возвращает уровень, соответствующий баллу.
    /// </summary>
    /// <param name="score">Итоговый балл.</param>
    /// <returns>Уровень вовлечённости.</returns>
    public static EngagementTier ResolveTier(double score)
    {
        var tier = Tiers[0];

        foreach (var candidate in Tiers)
        {
            if (score >= candidate.MinScore)
            {
                tier = candidate;
            }
        }

        return tier;
    }

    /// <summary>
    /// Возвращает следующий уровень для балла.
    /// </summary>
    /// <param name="score">Итоговый балл.</param>
    /// <returns>Следующий уровень или <c>null</c>, если достигнут максимальный.</returns>
    public static EngagementTier? NextTier(double score)
    {
        foreach (var candidate in Tiers)
        {
            if (candidate.MinScore > score)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Округляет баллы до одного знака после запятой.
    /// </summary>
    /// <param name="value">Значение.</param>
    /// <returns>Округлённое значение.</returns>
    public static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
