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
/// <param name="AllowAutomatic">Разрешить предлагать автоматические шаги.</param>
public sealed record AiQuestStepRequest(
    string QuestTitle,
    string? QuestDescription = null,
    int StepsCount = 4,
    string? ProofHint = null,
    bool AllowAutomatic = true);

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

/// <summary>
/// Параметры генерации полного черновика квеста по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт администратора.</param>
/// <param name="RewardAmount">Награда из формы; обязательна и не придумывается моделью.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="StepsCount">Количество шагов, 1–8.</param>
/// <param name="AllowAutomatic">Разрешить автоматические шаги.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record AiQuestActivityDraftRequest(
    string Prompt,
    long? RewardAmount,
    DateTime? StartAt = null,
    DateTime? EndAt = null,
    bool IsActive = false,
    int StepsCount = 4,
    bool AllowAutomatic = true,
    string? Audience = null,
    string? Tone = null);

/// <summary>
/// Параметры генерации полного черновика мероприятия по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт администратора.</param>
/// <param name="StartAt">Начало из формы; обязательно.</param>
/// <param name="EndAt">Окончание из формы; обязательно.</param>
/// <param name="RewardAmount">Награда из формы; обязательна.</param>
/// <param name="Location">Место; необязательно.</param>
/// <param name="MaxParticipants">Лимит участников; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record AiEventActivityDraftRequest(
    string Prompt,
    DateTime? StartAt,
    DateTime? EndAt,
    long? RewardAmount,
    string? Location = null,
    int? MaxParticipants = null,
    bool IsActive = false,
    string? Audience = null,
    string? Tone = null);

/// <summary>
/// Параметры генерации полного черновика анкеты по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт администратора.</param>
/// <param name="RewardAmount">Награда из формы; обязательна.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="AllowMultipleResponses">Разрешить повторные ответы.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="QuestionsCount">Количество вопросов, 1–12.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record AiSurveyActivityDraftRequest(
    string Prompt,
    long? RewardAmount,
    DateTime? StartAt = null,
    DateTime? EndAt = null,
    bool AllowMultipleResponses = false,
    bool IsActive = false,
    int QuestionsCount = 5,
    string? Audience = null,
    string? Tone = null);

/// <summary>
/// Параметры генерации полного черновика товара по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт мерчандайзера.</param>
/// <param name="CategoryId">Категория из формы; необязательно.</param>
/// <param name="Price">Цена из формы; если не задана, её предлагает модель.</param>
/// <param name="Stock">Остаток из формы; необязательно.</param>
/// <param name="ImageUrl">Ссылка на изображение из формы; модель её не придумывает.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record AiProductDraftRequest(
    string Prompt,
    int? CategoryId = null,
    long? Price = null,
    int? Stock = null,
    string? ImageUrl = null,
    bool IsActive = false,
    string? Tone = null);

/// <summary>Черновик квеста: название и описание.</summary>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
public sealed record QuestTextDraft(string Name, string Description);

/// <summary>Черновик шага квеста.</summary>
/// <param name="Title">Название шага.</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип выполнения: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="ActionType">Код автоматического действия или <c>null</c>.</param>
/// <param name="TargetId">Идентификатор выбранного объекта из списка Backend-а.</param>
/// <param name="AchievementCode">Код достижения из списка Backend-а.</param>
/// <param name="NeedsConfiguration">
/// Признак того, что модель предложила автоматический шаг, но объект не выбран:
/// администратор должен заполнить параметр.
/// </param>
/// <summary>
/// Типизированные параметры автоматического шага в черновике.
/// </summary>
/// <param name="Type">Код действия.</param>
/// <param name="TargetId">Идентификатор выбранного объекта из списка Backend-а.</param>
/// <param name="AchievementCode">Код достижения из списка Backend-а.</param>
/// <param name="ProductId">Товар для <c>make_purchase</c>.</param>
/// <param name="MinAmount">Минимальная сумма заказа для <c>make_purchase</c>.</param>
/// <param name="Count">Количество благодарностей.</param>
/// <param name="RequireAvatar">Требование аватара для <c>complete_profile</c>.</param>
public sealed record QuestStepActionDraft(
    string Type,
    long? TargetId = null,
    string? AchievementCode = null,
    long? ProductId = null,
    long? MinAmount = null,
    int? Count = null,
    bool? RequireAvatar = null);

/// <summary>Черновик шага квеста, совместимый с <c>CreateQuestStepRequest</c>.</summary>
/// <param name="Title">Название шага.</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип выполнения: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="SortOrder">Порядок; назначается Backend-ом.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="Action">Типизированные параметры автоматического шага.</param>
/// <param name="CompletionData">Устаревшее JSON-представление; заполняется только для совместимости.</param>
/// <param name="NeedsConfiguration">Признак необходимости ручной настройки.</param>
public sealed record QuestStepDraft(
    string Title,
    string Description,
    string CompletionType,
    int SortOrder,
    bool IsRequired,
    QuestStepActionDraft? Action = null,
    string? CompletionData = null,
    bool NeedsConfiguration = false);

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
public sealed record SurveyTextDraft(string Title, string Description);

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
    int SortOrder,
    bool IsRequired);

/// <summary>Полный черновик квеста, совместимый с <c>CreateQuestRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание; необязательно.</param>
/// <param name="RewardAmount">Награда из формы.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Steps">Шаги квеста.</param>
public sealed record QuestDraft(
    string Title,
    string? Description,
    long RewardAmount,
    DateTime? StartAt,
    DateTime? EndAt,
    bool IsActive,
    IReadOnlyList<QuestStepDraft> Steps);

/// <summary>Полный черновик мероприятия, совместимый с <c>CreateEventRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание; необязательно.</param>
/// <param name="StartAt">Начало из формы.</param>
/// <param name="EndAt">Окончание из формы.</param>
/// <param name="Location">Место; необязательно.</param>
/// <param name="MaxParticipants">Лимит участников; необязательно.</param>
/// <param name="RewardAmount">Награда из формы.</param>
/// <param name="IsActive">Признак активности из формы.</param>
public sealed record EventDraft(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    string? Location,
    int? MaxParticipants,
    long RewardAmount,
    bool IsActive);

/// <summary>Полный черновик анкеты, совместимый с <c>CreateSurveyRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание; необязательно.</param>
/// <param name="RewardAmount">Награда из формы.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="AllowMultipleResponses">Разрешить повторные ответы.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Questions">Вопросы анкеты.</param>
public sealed record SurveyDraft(
    string Title,
    string? Description,
    long RewardAmount,
    DateTime? StartAt,
    DateTime? EndAt,
    bool AllowMultipleResponses,
    bool IsActive,
    IReadOnlyList<SurveyQuestionDraft> Questions);

/// <summary>Происхождение цены товара в черновике.</summary>
/// <param name="Source">Источник: <c>user</c> или <c>ai</c>.</param>
/// <param name="RequiresConfirmation">Цену предложила модель, пользователь должен подтвердить.</param>
/// <param name="ReferenceMin">Минимальная цена реальных товаров категории; необязательно.</param>
/// <param name="ReferenceMax">Максимальная цена реальных товаров категории; необязательно.</param>
public sealed record PriceProvenance(
    string Source,
    bool RequiresConfirmation,
    long? ReferenceMin = null,
    long? ReferenceMax = null);

/// <summary>Полный черновик товара, совместимый с <c>CreateProductRequest</c>.</summary>
/// <param name="CategoryId">Категория из формы или реальная категория, выбранная моделью.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание; необязательно.</param>
/// <param name="Price">Цена; пользовательская или предложенная моделью.</param>
/// <param name="PriceProvenance">Происхождение цены для проверки на фронтенде.</param>
/// <param name="ImageUrl">Ссылка из формы; модель её не придумывает.</param>
/// <param name="Stock">Остаток из формы; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="IsDigital">Всегда <c>false</c>: цифровые товары запрещены.</param>
public sealed record ProductDraft(
    int? CategoryId,
    string Name,
    string? Description,
    long Price,
    PriceProvenance PriceProvenance,
    string? ImageUrl,
    int? Stock,
    bool IsActive,
    bool IsDigital);

/// <summary>Реальная категория каталога, передаваемая модели и форме.</summary>
/// <param name="Id">Идентификатор категории.</param>
/// <param name="Name">Название категории.</param>
public sealed record CategoryOption(int Id, string Name);

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
    Task<ServiceResult<AiContentResult<IReadOnlyList<QuestTextDraft>>>> GenerateQuestDraftsAsync(
        AiQuestDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует полный черновик квеста по свободному промпту.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновик квеста или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<QuestDraft>>> GenerateQuestActivityDraftAsync(
        AiQuestActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует полный черновик мероприятия по свободному промпту.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновик мероприятия или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<EventDraft>>> GenerateEventActivityDraftAsync(
        AiEventActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует полный черновик анкеты по свободному промпту.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновик анкеты или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<SurveyDraft>>> GenerateSurveyActivityDraftAsync(
        AiSurveyActivityDraftRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Генерирует полный черновик товара по свободному промпту.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="adminId">Идентификатор администратора для журнала.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Черновик товара или ошибка 400/502/503/504.</returns>
    Task<ServiceResult<AiContentResult<ProductDraft>>> GenerateProductDraftAsync(
        AiProductDraftRequest request,
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
    Task<ServiceResult<AiContentResult<IReadOnlyList<SurveyTextDraft>>>> GenerateSurveyDraftsAsync(
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
