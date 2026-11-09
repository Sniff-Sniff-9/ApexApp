using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.DTOs;

/// <summary>
/// Уровень вовлечённости пользователя.
/// </summary>
public sealed class EngagementStatusDto
{
    /// <summary>Стабильный машинный код уровня.</summary>
    public required string Code { get; init; }

    /// <summary>Название уровня для отображения.</summary>
    public required string Title { get; init; }

    /// <summary>Порядковый номер уровня: 1 — самый низкий, 5 — самый высокий.</summary>
    public required int Rank { get; init; }
}

/// <summary>
/// Следующий уровень вовлечённости.
/// </summary>
public sealed class EngagementNextLevelDto
{
    /// <summary>Стабильный машинный код следующего уровня.</summary>
    public required string Code { get; init; }

    /// <summary>Название следующего уровня для отображения.</summary>
    public required string Title { get; init; }

    /// <summary>Балл, необходимый для перехода на следующий уровень.</summary>
    public required double ScoreRequired { get; init; }

    /// <summary>Сколько баллов не хватает до следующего уровня.</summary>
    public required double PointsToNext { get; init; }

    /// <summary>Прогресс внутри текущего уровня к следующему: от 0 до 1.</summary>
    public required double Progress { get; init; }
}

/// <summary>
/// Расчётное окно статуса вовлечённости.
/// </summary>
public sealed class EngagementPeriodDto
{
    /// <summary>Длина окна в днях.</summary>
    public required int Days { get; init; }

    /// <summary>Начало окна (UTC).</summary>
    public required DateTime From { get; init; }

    /// <summary>Конец окна, момент расчёта (UTC).</summary>
    public required DateTime To { get; init; }
}

/// <summary>
/// Вклад одного источника активности в итоговый балл.
/// </summary>
public sealed class EngagementBreakdownDto
{
    /// <summary>Машинный код источника.</summary>
    public required string Code { get; init; }

    /// <summary>Название источника для отображения.</summary>
    public required string Title { get; init; }

    /// <summary>Количество учтённых действий.</summary>
    public required int Count { get; init; }

    /// <summary>Начисленные баллы за источник.</summary>
    public required double Points { get; init; }

    /// <summary>Максимум баллов по источнику.</summary>
    public required double MaxPoints { get; init; }
}

/// <summary>
/// Статус вовлечённости текущего пользователя за скользящее окно.
/// </summary>
public sealed class EngagementDto
{
    /// <summary>Текущий уровень вовлечённости.</summary>
    public required EngagementStatusDto Status { get; init; }

    /// <summary>Итоговый балл вовлечённости (0–100).</summary>
    public required double Score { get; init; }

    /// <summary>Максимально возможный балл.</summary>
    public required double MaxScore { get; init; }

    /// <summary>Следующий уровень; <c>null</c>, если достигнут максимальный.</summary>
    public EngagementNextLevelDto? NextLevel { get; init; }

    /// <summary>Расчётное окно.</summary>
    public required EngagementPeriodDto Period { get; init; }

    /// <summary>Последнее учтённое действие внутри окна; <c>null</c>, если активности нет.</summary>
    public DateTime? LastActivityAt { get; init; }

    /// <summary>
    /// Разбивка баллов по источникам активности. Итоговый балл равен
    /// <c>min(100, min(events + quests, 45) + steps + surveys + thanksReceived + thanksSent + variety)</c>.
    /// </summary>
    public required IReadOnlyList<EngagementBreakdownDto> Breakdown { get; init; }

    /// <summary>Момент расчёта (UTC).</summary>
    public required DateTime ComputedAt { get; init; }

    /// <summary>
    /// Создаёт DTO из результата расчёта вовлечённости.
    /// </summary>
    /// <param name="summary">Рассчитанный статус вовлечённости.</param>
    /// <returns>DTO для ответа API.</returns>
    public static EngagementDto From(EngagementSummary summary)
    {
        return new EngagementDto
        {
            Status = new EngagementStatusDto
            {
                Code = summary.Status,
                Title = summary.StatusTitle,
                Rank = summary.StatusRank
            },
            Score = summary.Score,
            MaxScore = summary.MaxScore,
            NextLevel = summary.NextStatus is null || summary.NextStatusTitle is null
                ? null
                : new EngagementNextLevelDto
                {
                    Code = summary.NextStatus,
                    Title = summary.NextStatusTitle,
                    ScoreRequired = summary.NextStatusScore ?? summary.MaxScore,
                    PointsToNext = summary.PointsToNext ?? 0d,
                    Progress = summary.NextLevelProgress
                },
            Period = new EngagementPeriodDto
            {
                Days = summary.WindowDays,
                From = summary.WindowStart,
                To = summary.WindowEnd
            },
            LastActivityAt = summary.LastActivityAt,
            Breakdown = summary.Breakdown
                .Select(item => new EngagementBreakdownDto
                {
                    Code = item.Code,
                    Title = item.Title,
                    Count = item.Count,
                    Points = item.Points,
                    MaxPoints = item.MaxPoints
                })
                .ToList(),
            ComputedAt = summary.ComputedAt
        };
    }
}
