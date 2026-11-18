using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>Черновик названия и описания квеста.</summary>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
public sealed record QuestTextDraftDto(string Name, string Description)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static QuestTextDraftDto From(QuestTextDraft draft) =>
        new(draft.Name, draft.Description);
}

/// <summary>Типизированные параметры автоматического шага в черновике.</summary>
/// <param name="Type">Код действия.</param>
/// <param name="TargetId">Идентификатор выбранного объекта.</param>
/// <param name="AchievementCode">Код достижения.</param>
/// <param name="ProductId">Товар для покупки.</param>
/// <param name="MinAmount">Минимальная сумма заказа.</param>
/// <param name="Count">Количество благодарностей.</param>
/// <param name="RequireAvatar">Требование аватара.</param>
public sealed record QuestStepActionDraftDto(
    string Type,
    long? TargetId = null,
    string? AchievementCode = null,
    long? ProductId = null,
    long? MinAmount = null,
    int? Count = null,
    bool? RequireAvatar = null)
{
    /// <summary>Создаёт DTO из черновика действия.</summary>
    /// <param name="action">Черновик действия.</param>
    /// <returns>DTO действия.</returns>
    public static QuestStepActionDraftDto From(QuestStepActionDraft action) =>
        new(
            action.Type,
            action.TargetId,
            action.AchievementCode,
            action.ProductId,
            action.MinAmount,
            action.Count,
            action.RequireAvatar);
}

/// <summary>Черновик шага квеста, совместимый с <c>CreateQuestStepRequest</c>.</summary>
/// <param name="Title">Название шага.</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип выполнения.</param>
/// <param name="SortOrder">Порядок, назначенный Backend-ом.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="Action">Типизированные параметры автоматического шага.</param>
/// <param name="CompletionData">Устаревшее JSON-представление.</param>
/// <param name="NeedsConfiguration">Признак ручной настройки.</param>
public sealed record QuestStepDraftDto(
    string Title,
    string Description,
    string CompletionType,
    int SortOrder,
    bool IsRequired,
    QuestStepActionDraftDto? Action = null,
    string? CompletionData = null,
    bool NeedsConfiguration = false)
{
    /// <summary>Создаёт DTO из черновика шага.</summary>
    /// <param name="draft">Черновик шага.</param>
    /// <returns>DTO черновика.</returns>
    public static QuestStepDraftDto From(QuestStepDraft draft) =>
        new(
            draft.Title,
            draft.Description,
            draft.CompletionType,
            draft.SortOrder,
            draft.IsRequired,
            draft.Action is null ? null : QuestStepActionDraftDto.From(draft.Action),
            draft.CompletionData,
            draft.NeedsConfiguration);
}

/// <summary>Черновик названия и описания анкеты.</summary>
/// <param name="Title">Название анкеты.</param>
/// <param name="Description">Описание анкеты.</param>
public sealed record SurveyTextDraftDto(string Title, string Description)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static SurveyTextDraftDto From(SurveyTextDraft draft) =>
        new(draft.Title, draft.Description);
}

/// <summary>Полный черновик квеста, совместимый с <c>CreateQuestRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="RewardAmount">Награда.</param>
/// <param name="StartAt">Начало.</param>
/// <param name="EndAt">Окончание.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="Steps">Шаги.</param>
public sealed record QuestDraftDto(
    string Title,
    string? Description,
    long RewardAmount,
    DateTime? StartAt,
    DateTime? EndAt,
    bool IsActive,
    IReadOnlyList<QuestStepDraftDto> Steps)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static QuestDraftDto From(QuestDraft draft) =>
        new(
            draft.Title,
            draft.Description,
            draft.RewardAmount,
            draft.StartAt,
            draft.EndAt,
            draft.IsActive,
            draft.Steps.Select(QuestStepDraftDto.From).ToList());
}

/// <summary>Полный черновик мероприятия, совместимый с <c>CreateEventRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="StartAt">Начало.</param>
/// <param name="EndAt">Окончание.</param>
/// <param name="Location">Место.</param>
/// <param name="MaxParticipants">Лимит участников.</param>
/// <param name="RewardAmount">Награда.</param>
/// <param name="IsActive">Признак активности.</param>
public sealed record EventDraftDto(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    string? Location,
    int? MaxParticipants,
    long RewardAmount,
    bool IsActive)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static EventDraftDto From(EventDraft draft) =>
        new(
            draft.Title,
            draft.Description,
            draft.StartAt,
            draft.EndAt,
            draft.Location,
            draft.MaxParticipants,
            draft.RewardAmount,
            draft.IsActive);
}

/// <summary>Полный черновик анкеты, совместимый с <c>CreateSurveyRequest</c>.</summary>
/// <param name="Title">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="RewardAmount">Награда.</param>
/// <param name="StartAt">Начало.</param>
/// <param name="EndAt">Окончание.</param>
/// <param name="AllowMultipleResponses">Повторные ответы.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="Questions">Вопросы.</param>
public sealed record SurveyDraftDto(
    string Title,
    string? Description,
    long RewardAmount,
    DateTime? StartAt,
    DateTime? EndAt,
    bool AllowMultipleResponses,
    bool IsActive,
    IReadOnlyList<SurveyQuestionDraftDto> Questions)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static SurveyDraftDto From(SurveyDraft draft) =>
        new(
            draft.Title,
            draft.Description,
            draft.RewardAmount,
            draft.StartAt,
            draft.EndAt,
            draft.AllowMultipleResponses,
            draft.IsActive,
            draft.Questions.Select(SurveyQuestionDraftDto.From).ToList());
}

/// <summary>Происхождение цены товара в черновике.</summary>
/// <param name="Source">Источник: <c>user</c> или <c>ai</c>.</param>
/// <param name="RequiresConfirmation">Цену нужно подтвердить.</param>
/// <param name="ReferenceMin">Минимальная цена категории.</param>
/// <param name="ReferenceMax">Максимальная цена категории.</param>
public sealed record PriceProvenanceDto(
    string Source,
    bool RequiresConfirmation,
    long? ReferenceMin = null,
    long? ReferenceMax = null)
{
    /// <summary>Создаёт DTO из происхождения цены.</summary>
    /// <param name="provenance">Происхождение цены.</param>
    /// <returns>DTO происхождения.</returns>
    public static PriceProvenanceDto From(PriceProvenance provenance) =>
        new(
            provenance.Source,
            provenance.RequiresConfirmation,
            provenance.ReferenceMin,
            provenance.ReferenceMax);
}

/// <summary>Полный черновик товара, совместимый с <c>CreateProductRequest</c>.</summary>
/// <param name="CategoryId">Категория.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Price">Цена.</param>
/// <param name="PriceProvenance">Происхождение цены.</param>
/// <param name="ImageUrl">Ссылка на изображение.</param>
/// <param name="Stock">Остаток.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="IsDigital">Всегда false.</param>
public sealed record ProductDraftDto(
    int? CategoryId,
    string Name,
    string? Description,
    long Price,
    PriceProvenanceDto PriceProvenance,
    string? ImageUrl,
    int? Stock,
    bool IsActive,
    bool IsDigital)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static ProductDraftDto From(ProductDraft draft) =>
        new(
            draft.CategoryId,
            draft.Name,
            draft.Description,
            draft.Price,
            PriceProvenanceDto.From(draft.PriceProvenance),
            draft.ImageUrl,
            draft.Stock,
            draft.IsActive,
            draft.IsDigital);
}

/// <summary>Черновик вопроса анкеты.</summary>
/// <param name="QuestionText">Текст вопроса.</param>
/// <param name="QuestionType">
/// Тип вопроса: <c>text</c>, <c>single_choice</c>, <c>multiple_choice</c>,
/// <c>number</c>, <c>boolean</c> или <c>rating</c>.
/// </param>
/// <param name="Options">Варианты ответа; заполнены только для типов-выборов.</param>
/// <param name="IsRequired">Признак обязательного вопроса.</param>
public sealed record SurveyQuestionDraftDto(
    string QuestionText,
    string QuestionType,
    IReadOnlyList<string> Options,
    int SortOrder,
    bool IsRequired)
{
    /// <summary>Создаёт DTO из черновика вопроса.</summary>
    /// <param name="draft">Черновик вопроса.</param>
    /// <returns>DTO черновика вопроса.</returns>
    public static SurveyQuestionDraftDto From(SurveyQuestionDraft draft) =>
        new(
            draft.QuestionText,
            draft.QuestionType,
            draft.Options,
            draft.SortOrder,
            draft.IsRequired);
}

/// <summary>Черновик анонса мероприятия.</summary>
/// <param name="Title">Заголовок.</param>
/// <param name="Summary">Короткое описание для карточки.</param>
/// <param name="Body">Полный текст анонса.</param>
/// <param name="CallToAction">Призыв к действию.</param>
public sealed record EventAnnouncementDto(
    string Title,
    string Summary,
    string Body,
    string? CallToAction)
{
    /// <summary>Создаёт DTO из черновика анонса.</summary>
    /// <param name="announcement">Черновик анонса.</param>
    /// <returns>DTO черновика.</returns>
    public static EventAnnouncementDto From(EventAnnouncement announcement) =>
        new(
            announcement.Title,
            announcement.Summary,
            announcement.Body,
            announcement.CallToAction);
}

/// <summary>Черновик описания награды.</summary>
/// <param name="Name">Название награды.</param>
/// <param name="Description">Описание.</param>
/// <param name="ShortPitch">Короткий питч для карточки.</param>
public sealed record RewardDescriptionDto(
    string Name,
    string Description,
    string ShortPitch)
{
    /// <summary>Создаёт DTO из черновика награды.</summary>
    /// <param name="description">Черновик награды.</param>
    /// <returns>DTO черновика.</returns>
    public static RewardDescriptionDto From(RewardDescription description) =>
        new(description.Name, description.Description, description.ShortPitch);
}

/// <summary>Улучшенный текст.</summary>
/// <param name="Text">Итоговый текст.</param>
/// <param name="Changes">Что изменено.</param>
public sealed record ImprovedTextDto(
    string Text,
    IReadOnlyList<string> Changes)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="improved">Улучшенный текст.</param>
    /// <returns>DTO результата.</returns>
    public static ImprovedTextDto From(ImprovedText improved) =>
        new(improved.Text, improved.Changes);
}

/// <summary>Альтернативная версия текста.</summary>
/// <param name="Label">Короткая подпись варианта.</param>
/// <param name="Text">Текст варианта.</param>
public sealed record TextAlternativeDto(string Label, string Text)
{
    /// <summary>Создаёт DTO из результата сервиса.</summary>
    /// <param name="alternative">Альтернатива.</param>
    /// <returns>DTO альтернативы.</returns>
    public static TextAlternativeDto From(TextAlternative alternative) =>
        new(alternative.Label, alternative.Text);
}
