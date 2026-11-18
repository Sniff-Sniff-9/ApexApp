using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>Черновик квеста.</summary>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
public sealed record QuestDraftDto(string Name, string Description)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static QuestDraftDto From(QuestDraft draft) => new(draft.Name, draft.Description);
}

/// <summary>Черновик шага квеста.</summary>
/// <param name="Title">Название шага.</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип выполнения: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="ActionType">Код автоматического действия или <c>null</c>.</param>
/// <param name="TargetId">Идентификатор выбранного объекта.</param>
/// <param name="AchievementCode">Код достижения.</param>
/// <param name="NeedsConfiguration">
/// Признак того, что для автоматического шага нужно выбрать объект вручную.
/// </param>
public sealed record QuestStepDraftDto(
    string Title,
    string Description,
    string CompletionType,
    bool IsRequired,
    string? ActionType = null,
    long? TargetId = null,
    string? AchievementCode = null,
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
            draft.IsRequired,
            draft.ActionType,
            draft.TargetId,
            draft.AchievementCode,
            draft.NeedsConfiguration);
}

/// <summary>Черновик анкеты.</summary>
/// <param name="Title">Название анкеты.</param>
/// <param name="Description">Описание анкеты.</param>
public sealed record SurveyDraftDto(string Title, string Description)
{
    /// <summary>Создаёт DTO из черновика.</summary>
    /// <param name="draft">Черновик.</param>
    /// <returns>DTO черновика.</returns>
    public static SurveyDraftDto From(SurveyDraft draft) => new(draft.Title, draft.Description);
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
    bool IsRequired)
{
    /// <summary>Создаёт DTO из черновика вопроса.</summary>
    /// <param name="draft">Черновик вопроса.</param>
    /// <returns>DTO черновика вопроса.</returns>
    public static SurveyQuestionDraftDto From(SurveyQuestionDraft draft) =>
        new(draft.QuestionText, draft.QuestionType, draft.Options, draft.IsRequired);
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
