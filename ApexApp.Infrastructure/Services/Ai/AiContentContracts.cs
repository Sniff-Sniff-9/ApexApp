using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Параметры генерации названия и описания квеста.
/// </summary>
/// <param name="Topic">Тема или повод; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="RewardAmount">Награда в баллах из формы квеста; необязательно.</param>
/// <param name="DurationDays">Ориентировочная длительность в днях; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record AiQuestDraftRequest(
    string? Topic,
    string? Audience,
    long? RewardAmount,
    int? DurationDays,
    string? Tone,
    int Variants = 1);

/// <summary>
/// Параметры генерации шагов квеста.
/// </summary>
/// <param name="QuestTitle">Название квеста.</param>
/// <param name="QuestDescription">Описание квеста; необязательно.</param>
/// <param name="StepsCount">Количество шагов, 1–8.</param>
/// <param name="ProofHint">Подсказка о доказательствах; необязательно.</param>
public sealed record AiQuestStepRequest(
    string QuestTitle,
    string? QuestDescription = null,
    int StepsCount = 4,
    string? ProofHint = null);

/// <summary>
/// Параметры генерации анонса корпоративного мероприятия.
/// </summary>
/// <param name="Title">Рабочее название мероприятия.</param>
/// <param name="Location">Место проведения; необязательно.</param>
/// <param name="StartAt">Дата и время начала; необязательно.</param>
/// <param name="RewardAmount">Награда за посещение в баллах; необязательно.</param>
/// <param name="MaxParticipants">Ограничение участников; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record AiEventAnnouncementRequest(
    string Title,
    string? Location = null,
    DateTime? StartAt = null,
    long? RewardAmount = null,
    int? MaxParticipants = null,
    string? Audience = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Параметры генерации названия и описания награды.
/// </summary>
/// <param name="ItemName">Название или тип награды.</param>
/// <param name="Category">Категория каталога; необязательно.</param>
/// <param name="Price">Цена в баллах; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record AiRewardDescriptionRequest(
    string ItemName,
    string? Category = null,
    long? Price = null,
    string? Audience = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Параметры улучшения существующего текста.
/// </summary>
/// <param name="Text">Исходный текст.</param>
/// <param name="TextKind">Тип текста; необязательно.</param>
/// <param name="Instruction">Пожелание администратора; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
public sealed record AiImproveTextRequest(
    string Text,
    string? TextKind = null,
    string? Instruction = null,
    string? Tone = null);

/// <summary>
/// Параметры генерации альтернативных версий текста.
/// </summary>
/// <param name="Text">Исходный текст.</param>
/// <param name="TextKind">Тип текста; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record AiAlternativesRequest(
    string Text,
    string? TextKind = null,
    string? Tone = null,
    int Variants = 2);

/// <summary>
/// Параметры генерации названия и описания анкеты.
/// </summary>
/// <param name="Topic">Тема или цель анкеты; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="RewardAmount">Награда в баллах из формы анкеты; необязательно.</param>
/// <param name="QuestionsCount">Ориентировочное количество вопросов; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record AiSurveyDraftRequest(
    string? Topic,
    string? Audience,
    long? RewardAmount,
    int? QuestionsCount,
    string? Tone,
    int Variants = 1);

/// <summary>
/// Параметры генерации вопросов анкеты.
/// </summary>
/// <param name="SurveyTitle">Название анкеты.</param>
/// <param name="SurveyDescription">Описание анкеты; необязательно.</param>
/// <param name="QuestionsCount">Количество вопросов, 1–12.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
public sealed record AiSurveyQuestionsRequest(
    string SurveyTitle,
    string? SurveyDescription = null,
    int QuestionsCount = 5,
    string? Audience = null);

/// <summary>Черновик квеста: название и описание.</summary>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
public sealed record QuestDraft(string Name, string Description);

/// <summary>Черновик шага квеста.</summary>
/// <param name="Title">Название шага.</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип выполнения: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
public sealed record QuestStepDraft(
    string Title,
    string Description,
    string CompletionType,
    bool IsRequired);

/// <summary>Черновик анонса мероприятия.</summary>
/// <param name="Title">Заголовок.</param>
/// <param name="Summary">Короткое описание.</param>
/// <param name="Body">Полный текст.</param>
/// <param name="CallToAction">Призыв к действию.</param>
public sealed record EventAnnouncement(
    string Title,
    string Summary,
    string Body,
    string? CallToAction);

/// <summary>Черновик описания награды.</summary>
/// <param name="Name">Название награды.</param>
/// <param name="Description">Описание.</param>
/// <param name="ShortPitch">Короткий питч.</param>
public sealed record RewardDescription(string Name, string Description, string ShortPitch);

/// <summary>Улучшенный текст с перечнем изменений.</summary>
/// <param name="Text">Итоговый текст.</param>
/// <param name="Changes">Что изменено.</param>
public sealed record ImprovedText(string Text, IReadOnlyList<string> Changes);

/// <summary>Черновик анкеты: название и описание.</summary>
/// <param name="Title">Название анкеты.</param>
/// <param name="Description">Описание анкеты.</param>
public sealed record SurveyDraft(string Title, string Description);

/// <summary>Черновик вопроса анкеты.</summary>
/// <param name="QuestionText">Текст вопроса.</param>
/// <param name="QuestionType">
/// Тип вопроса: <c>text</c>, <c>single_choice</c>, <c>multiple_choice</c>,
/// <c>number</c>, <c>boolean</c> или <c>rating</c>.
/// </param>
/// <param name="Options">Варианты ответа; заполнены только для типов-выборов.</param>
/// <param name="IsRequired">Признак обязательного вопроса.</param>
public sealed record SurveyQuestionDraft(
    string QuestionText,
    string QuestionType,
    IReadOnlyList<string> Options,
    bool IsRequired);

/// <summary>Альтернативная версия текста.</summary>
/// <param name="Label">Короткая подпись варианта.</param>
/// <param name="Text">Текст варианта.</param>
public sealed record TextAlternative(string Label, string Text);

/// <summary>Служебные метрики генерации.</summary>
/// <param name="Model">Использованная модель.</param>
/// <param name="GeneratedAt">Момент генерации (UTC).</param>
/// <param name="PromptTokens">Токены запроса.</param>
/// <param name="CompletionTokens">Токены ответа.</param>
/// <param name="Truncated">Признак обрыва ответа по лимиту токенов.</param>
public sealed record AiGenerationMeta(
    string Model,
    DateTime GeneratedAt,
    long PromptTokens,
    long CompletionTokens,
    bool Truncated)
{
    /// <summary>Создаёт метрики из ответа провайдера.</summary>
    /// <param name="completion">Ответ провайдера.</param>
    /// <returns>Метрики генерации.</returns>
    public static AiGenerationMeta Create(AiCompletion completion) =>
        new(
            completion.Model,
            DateTime.UtcNow,
            completion.PromptTokens,
            completion.CompletionTokens,
            completion.Truncated);
}

/// <summary>Результат генерации: содержимое и метрики.</summary>
/// <typeparam name="T">Тип содержимого.</typeparam>
/// <param name="Content">Содержимое.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record AiContentResult<T>(T Content, AiGenerationMeta Meta);

/// <summary>
/// Генерация черновиков для административной панели. Сервис не сохраняет результаты
/// и не публикует их: публикация остаётся за администратором.
/// </summary>
public interface IAiContentService
{
    /// <summary>Генерирует варианты названия и описания квеста.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<QuestDraft>>>> GenerateQuestDraftsAsync(
        AiQuestDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует шаги квеста.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики шагов или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<QuestStepDraft>>>> GenerateQuestStepsAsync(
        AiQuestStepRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует анонс корпоративного мероприятия.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики анонса или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<EventAnnouncement>>>> GenerateEventAnnouncementsAsync(
        AiEventAnnouncementRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует название и описание награды.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики награды или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<RewardDescription>>>> GenerateRewardDescriptionsAsync(
        AiRewardDescriptionRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует название и описание анкеты.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики анкеты или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyDraft>>>> GenerateSurveyDraftsAsync(
        AiSurveyDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует вопросы анкеты.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновики вопросов или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyQuestionDraft>>>> GenerateSurveyQuestionsAsync(
        AiSurveyQuestionsRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Улучшает существующий текст.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Улучшенный текст или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<ImprovedText>>> ImproveTextAsync(
        AiImproveTextRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует альтернативные версии текста.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Альтернативы или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<IReadOnlyList<TextAlternative>>>> GenerateAlternativesAsync(
        AiAlternativesRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);
}
