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

/// <summary>Идея активности.</summary>
/// <param name="Kind">Формат активности.</param>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="WhyItFits">Почему идея подходит данным.</param>
/// <param name="Effort">Трудоёмкость.</param>
/// <param name="ExpectedImpact">Ожидаемый эффект.</param>
public sealed record ActivityIdeaDto(
    string Kind,
    string Title,
    string Description,
    string WhyItFits,
    string Effort,
    string ExpectedImpact)
{
    /// <summary>Создаёт DTO из идеи.</summary>
    /// <param name="idea">Идея активности.</param>
    /// <returns>DTO идеи.</returns>
    public static ActivityIdeaDto From(ActivityIdea idea) =>
        new(
            idea.Kind,
            idea.Title,
            idea.Description,
            idea.WhyItFits,
            idea.Effort,
            idea.ExpectedImpact);
}

/// <summary>Идея награды.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Rationale">Обоснование по данным.</param>
/// <param name="SuggestedPriceBand">Ориентировочный диапазон цены в баллах.</param>
public sealed record RewardIdeaDto(
    string Title,
    string Description,
    string Rationale,
    string? SuggestedPriceBand)
{
    /// <summary>Создаёт DTO из идеи награды.</summary>
    /// <param name="idea">Идея награды.</param>
    /// <returns>DTO идеи.</returns>
    public static RewardIdeaDto From(RewardIdea idea) =>
        new(idea.Title, idea.Description, idea.Rationale, idea.SuggestedPriceBand);
}

/// <summary>Отчёт рекомендаций по вовлечённости.</summary>
/// <param name="Engagement">Показатели, переданные модели.</param>
/// <param name="Analysis">Краткий анализ.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="ActivityIdeas">Идеи активностей.</param>
/// <param name="RewardIdeas">Идеи наград.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
public sealed record EngagementRecommendationDto(
    EngagementSummaryDto Engagement,
    string Analysis,
    IReadOnlyList<AiFindingDto> Findings,
    IReadOnlyList<ActivityIdeaDto> ActivityIdeas,
    IReadOnlyList<RewardIdeaDto> RewardIdeas,
    IReadOnlyList<string> Limitations)
{
    /// <summary>Создаёт DTO из отчёта сервиса.</summary>
    /// <param name="report">Отчёт.</param>
    /// <returns>DTO отчёта.</returns>
    public static EngagementRecommendationDto From(EngagementRecommendationReport report) =>
        new(
            EngagementSummaryDto.From(report.Engagement),
            report.Analysis,
            report.Findings.Select(AiFindingDto.From).ToList(),
            report.ActivityIdeas.Select(ActivityIdeaDto.From).ToList(),
            report.RewardIdeas.Select(RewardIdeaDto.From).ToList(),
            report.Limitations);
}
