using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>
/// Состояние модуля ИИ. Ключ API не раскрывается.
/// </summary>
/// <param name="Configured">Признак заполненной конфигурации.</param>
/// <param name="Model">Настроенная модель.</param>
/// <param name="BaseUrlHost">Хост провайдера.</param>
/// <param name="TimeoutSeconds">Таймаут запроса в секундах.</param>
public sealed record AiStatusDto(
    bool Configured,
    string Model,
    string BaseUrlHost,
    int TimeoutSeconds);

/// <summary>Наблюдение правил аналитики, на которое опиралась модель.</summary>
/// <param name="Code">Машинный код правила.</param>
/// <param name="Severity">Важность.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Description">Описание.</param>
public sealed record AiFindingDto(
    string Code,
    string Severity,
    string Title,
    string Description)
{
    /// <summary>Создаёт DTO из наблюдения.</summary>
    /// <param name="finding">Наблюдение.</param>
    /// <returns>DTO наблюдения.</returns>
    public static AiFindingDto From(AiFinding finding) =>
        new(finding.Code, finding.Severity, finding.Title, finding.Description);
}

/// <summary>Сравнение формата активности в сводке.</summary>
/// <param name="Format">Машинный код формата.</param>
/// <param name="Title">Название формата.</param>
/// <param name="Activities">Количество активностей.</param>
/// <param name="Participants">Количество участников.</param>
/// <param name="ReachRate">Доля охвата.</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="RewardClaimRate">Доля полученных наград.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record AiFormatSummaryDto(
    string Format,
    string Title,
    long Activities,
    long Participants,
    double? ReachRate,
    double? CompletionRate,
    double? RewardClaimRate,
    bool IsReliable)
{
    /// <summary>Создаёт DTO из сводки формата.</summary>
    /// <param name="format">Сводка формата.</param>
    /// <returns>DTO сводки.</returns>
    public static AiFormatSummaryDto From(AiFormatSnapshot format) =>
        new(
            format.Format,
            format.Title,
            format.Activities,
            format.Participants,
            format.ReachRate,
            format.CompletionRate,
            format.RewardClaimRate,
            format.IsReliable);
}

/// <summary>Существующая активность, переданная модели.</summary>
/// <param name="Format">Формат.</param>
/// <param name="Id">Идентификатор.</param>
/// <param name="Title">Название.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="Participants">Участники.</param>
/// <param name="Completed">Завершения.</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="RewardAmount">Награда.</param>
/// <param name="StartsAt">Начало.</param>
/// <param name="EndsAt">Окончание.</param>
public sealed record AiActivitySummaryDto(
    string Format,
    long Id,
    string Title,
    bool IsActive,
    long Participants,
    long Completed,
    double? CompletionRate,
    long RewardAmount,
    DateTime? StartsAt,
    DateTime? EndsAt)
{
    /// <summary>Создаёт DTO из строки активности.</summary>
    /// <param name="activity">Строка активности.</param>
    /// <returns>DTO активности.</returns>
    public static AiActivitySummaryDto From(AiActivityRow activity) =>
        new(
            activity.Format,
            activity.Id,
            activity.Title,
            activity.IsActive,
            activity.Participants,
            activity.Completed,
            activity.CompletionRate,
            activity.RewardAmount,
            activity.StartsAt,
            activity.EndsAt);
}

/// <summary>Показатели вовлечённости, переданные модели.</summary>
/// <param name="Period">Описание окна расчёта.</param>
/// <param name="ActiveEmployees">Активные сотрудники.</param>
/// <param name="ParticipatingEmployees">Участники активностей.</param>
/// <param name="ParticipationRate">Доля участия.</param>
/// <param name="AverageEngagementScore">Средний балл вовлечённости.</param>
/// <param name="ChangePoints">Изменение балла к предыдущему периоду.</param>
/// <param name="SilentEmployees">Сотрудники без действий в окне.</param>
/// <param name="DecliningEmployees">Сотрудники со снижением вовлечённости.</param>
/// <param name="Formats">Сравнение форматов.</param>
public sealed record EngagementSummaryDto(
    string Period,
    long ActiveEmployees,
    long ParticipatingEmployees,
    double? ParticipationRate,
    double? AverageEngagementScore,
    double? ChangePoints,
    long? SilentEmployees,
    long? DecliningEmployees,
    IReadOnlyList<AiFormatSummaryDto> Formats)
{
    /// <summary>Создаёт DTO из сводки вовлечённости.</summary>
    /// <param name="snapshot">Сводка.</param>
    /// <returns>DTO сводки.</returns>
    public static EngagementSummaryDto From(AiEngagementSnapshot snapshot) =>
        new(
            snapshot.Period,
            snapshot.ActiveEmployees,
            snapshot.ParticipatingEmployees,
            snapshot.ParticipationRate,
            snapshot.AverageEngagementScore,
            snapshot.ChangePoints,
            snapshot.SilentEmployees,
            snapshot.DecliningEmployees,
            snapshot.Formats.Select(AiFormatSummaryDto.From).ToList());
}

/// <summary>Рекомендация существующей активности.</summary>
/// <param name="Format">Формат.</param>
/// <param name="ActivityId">Идентификатор существующей активности.</param>
/// <param name="Title">Название из базы.</param>
/// <param name="Action">Рекомендуемое действие.</param>
/// <param name="Rationale">Обоснование по реальным показателям.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record ActivityRecommendationDto(
    string Format,
    long ActivityId,
    string Title,
    string Action,
    string Rationale,
    string ExpectedImpact)
{
    /// <summary>Создаёт DTO из рекомендации.</summary>
    /// <param name="recommendation">Рекомендация.</param>
    /// <returns>DTO рекомендации.</returns>
    public static ActivityRecommendationDto From(ActivityRecommendation recommendation) =>
        new(
            recommendation.Format,
            recommendation.ActivityId,
            recommendation.Title,
            recommendation.Action,
            recommendation.Rationale,
            recommendation.ExpectedImpact);
}

/// <summary>Отчёт рекомендаций по вовлечённости.</summary>
/// <param name="Engagement">Показатели, переданные модели.</param>
/// <param name="Activities">Существующие активности.</param>
/// <param name="Analysis">Краткий анализ.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="RecommendedActivities">Рекомендации существующих активностей.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
public sealed record EngagementRecommendationDto(
    EngagementSummaryDto Engagement,
    IReadOnlyList<AiActivitySummaryDto> Activities,
    string Analysis,
    IReadOnlyList<AiFindingDto> Findings,
    IReadOnlyList<ActivityRecommendationDto> RecommendedActivities,
    IReadOnlyList<string> Limitations)
{
    /// <summary>Создаёт DTO из отчёта сервиса.</summary>
    /// <param name="report">Отчёт.</param>
    /// <returns>DTO отчёта.</returns>
    public static EngagementRecommendationDto From(EngagementRecommendationReport report) =>
        new(
            EngagementSummaryDto.From(report.Engagement),
            report.Activities.Select(AiActivitySummaryDto.From).ToList(),
            report.Analysis,
            report.Findings.Select(AiFindingDto.From).ToList(),
            report.RecommendedActivities.Select(ActivityRecommendationDto.From).ToList(),
            report.Limitations);
}
