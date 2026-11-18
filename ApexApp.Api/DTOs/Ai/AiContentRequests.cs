namespace ApexApp.Api.DTOs.Ai;

/// <summary>
/// Запрос генерации названия и описания квеста.
/// </summary>
/// <param name="Topic">Тема или повод; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="RewardAmount">Награда в баллах из формы квеста; необязательно.</param>
/// <param name="DurationDays">Ориентировочная длительность в днях; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record GenerateQuestDraftRequest(
    string? Topic = null,
    string? Audience = null,
    long? RewardAmount = null,
    int? DurationDays = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Запрос генерации шагов квеста.
/// </summary>
/// <param name="QuestTitle">Название квеста.</param>
/// <param name="QuestDescription">Описание квеста; необязательно.</param>
/// <param name="StepsCount">Количество шагов, 1–8.</param>
/// <param name="ProofHint">Подсказка о доказательствах; необязательно.</param>
/// <param name="AllowAutomatic">Разрешить предлагать автоматические шаги.</param>
public sealed record GenerateQuestStepsRequest(
    string QuestTitle,
    string? QuestDescription = null,
    int StepsCount = 4,
    string? ProofHint = null,
    bool AllowAutomatic = true);

/// <summary>
/// Запрос генерации названия и описания анкеты.
/// </summary>
/// <param name="Topic">Тема или цель анкеты; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="RewardAmount">Награда в баллах из формы анкеты; необязательно.</param>
/// <param name="QuestionsCount">Ориентировочное количество вопросов; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record GenerateSurveyDraftRequest(
    string? Topic = null,
    string? Audience = null,
    long? RewardAmount = null,
    int? QuestionsCount = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Запрос генерации вопросов анкеты.
/// </summary>
/// <param name="SurveyTitle">Название анкеты.</param>
/// <param name="SurveyDescription">Описание анкеты; необязательно.</param>
/// <param name="QuestionsCount">Количество вопросов, 1–12.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
public sealed record GenerateSurveyQuestionsRequest(
    string SurveyTitle,
    string? SurveyDescription = null,
    int QuestionsCount = 5,
    string? Audience = null);

/// <summary>
/// Запрос генерации анонса корпоративного мероприятия.
/// </summary>
/// <param name="Title">Рабочее название мероприятия.</param>
/// <param name="Location">Место проведения; необязательно.</param>
/// <param name="StartAt">Дата и время начала; необязательно.</param>
/// <param name="RewardAmount">Награда за посещение в баллах; необязательно.</param>
/// <param name="MaxParticipants">Ограничение участников; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record GenerateEventAnnouncementRequest(
    string Title,
    string? Location = null,
    DateTime? StartAt = null,
    long? RewardAmount = null,
    int? MaxParticipants = null,
    string? Audience = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Запрос генерации названия и описания награды.
/// </summary>
/// <param name="ItemName">Название или тип награды.</param>
/// <param name="Category">Категория каталога; необязательно.</param>
/// <param name="Price">Цена в баллах; необязательно.</param>
/// <param name="Audience">Целевая аудитория; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record GenerateRewardDescriptionRequest(
    string ItemName,
    string? Category = null,
    long? Price = null,
    string? Audience = null,
    string? Tone = null,
    int Variants = 1);

/// <summary>
/// Запрос улучшения существующего текста.
/// </summary>
/// <param name="Text">Исходный текст.</param>
/// <param name="TextKind">Тип текста; необязательно.</param>
/// <param name="Instruction">Пожелание администратора; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
public sealed record ImproveTextRequest(
    string Text,
    string? TextKind = null,
    string? Instruction = null,
    string? Tone = null);

/// <summary>
/// Запрос генерации альтернативных версий текста.
/// </summary>
/// <param name="Text">Исходный текст.</param>
/// <param name="TextKind">Тип текста; необязательно.</param>
/// <param name="Tone">Желаемый тон; необязательно.</param>
/// <param name="Variants">Количество вариантов, 1–3.</param>
public sealed record GenerateAlternativesRequest(
    string Text,
    string? TextKind = null,
    string? Tone = null,
    int Variants = 2);

/// <summary>
/// Запрос полного черновика квеста по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт.</param>
/// <param name="RewardAmount">Награда из формы; обязательна.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="StepsCount">Количество шагов, 1–8.</param>
/// <param name="AllowAutomatic">Разрешить автоматические шаги.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record GenerateQuestActivityDraftRequest(
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
/// Запрос полного черновика мероприятия по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт.</param>
/// <param name="StartAt">Начало из формы; обязательно.</param>
/// <param name="EndAt">Окончание из формы; обязательно.</param>
/// <param name="RewardAmount">Награда из формы; обязательна.</param>
/// <param name="Location">Место; необязательно.</param>
/// <param name="MaxParticipants">Лимит участников; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record GenerateEventActivityDraftRequest(
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
/// Запрос полного черновика анкеты по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт.</param>
/// <param name="RewardAmount">Награда из формы; обязательна.</param>
/// <param name="StartAt">Начало из формы; необязательно.</param>
/// <param name="EndAt">Окончание из формы; необязательно.</param>
/// <param name="AllowMultipleResponses">Повторные ответы.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="QuestionsCount">Количество вопросов, 1–12.</param>
/// <param name="Audience">Аудитория; необязательно.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record GenerateSurveyActivityDraftRequest(
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
/// Запрос полного черновика товара по свободному промпту.
/// </summary>
/// <param name="Prompt">Свободный промпт.</param>
/// <param name="CategoryId">Категория из формы; необязательно.</param>
/// <param name="Price">Цена из формы; если не задана, предлагает модель.</param>
/// <param name="Stock">Остаток из формы; необязательно.</param>
/// <param name="ImageUrl">Ссылка на изображение из формы; необязательно.</param>
/// <param name="IsActive">Признак активности из формы.</param>
/// <param name="Tone">Тон; необязательно.</param>
public sealed record GenerateProductDraftRequest(
    string Prompt,
    int? CategoryId = null,
    long? Price = null,
    int? Stock = null,
    string? ImageUrl = null,
    bool IsActive = false,
    string? Tone = null);
