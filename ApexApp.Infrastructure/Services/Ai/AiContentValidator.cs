using System.Text.RegularExpressions;
using ApexApp.Infrastructure.Services.Quests;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Нормализация и ограничение ответов модели. Все тексты очищаются от markdown
/// и HTML, обрезаются по длине и дедуплицируются, чтобы администратор не получил
/// разметку и чтобы значения помещались в существующие поля сущностей.
/// </summary>
public static class AiContentValidator
{
    /// <summary>Максимальная длина названия: совпадает с ограничением сервисов квестов и мероприятий.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Максимальная длина описания.</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>Максимальная длина краткого описания новости.</summary>
    public const int SummaryMaxLength = 500;

    /// <summary>Максимальная длина полного текста анонса.</summary>
    public const int BodyMaxLength = 4000;

    /// <summary>Максимальная длина короткого питча.</summary>
    public const int ShortPitchMaxLength = 300;

    /// <summary>Максимальная длина призыва к действию.</summary>
    public const int CallToActionMaxLength = 160;

    /// <summary>Максимальная длина одного пункта изменений.</summary>
    public const int ChangeMaxLength = 200;

    /// <summary>Максимальное количество пунктов изменений.</summary>
    public const int ChangesMaxItems = 8;

    /// <summary>Максимальное количество шагов квеста.</summary>
    public const int MaxSteps = 8;

    /// <summary>Максимальное количество вариантов текста.</summary>
    public const int MaxVariants = 3;

    /// <summary>Максимальная длина улучшенного текста.</summary>
    public const int ImprovedTextMaxLength = 6000;

    /// <summary>Максимальное количество идей активности.</summary>
    public const int MaxActivityIdeas = 8;

    /// <summary>Максимальное количество идей наград.</summary>
    public const int MaxRewardIdeas = 6;

    /// <summary>Максимальное количество идей товаров.</summary>
    public const int MaxProductIdeas = 8;

    /// <summary>Максимальная длина текста вопроса анкеты.</summary>
    public const int QuestionTextMaxLength = 500;

    /// <summary>Максимальное количество вопросов анкеты.</summary>
    public const int MaxSurveyQuestions = 12;

    /// <summary>Максимальное количество вариантов ответа на вопрос.</summary>
    public const int MaxQuestionOptions = 8;

    /// <summary>Максимальная длина варианта ответа.</summary>
    public const int OptionMaxLength = 160;

    /// <summary>Допустимые форматы активности.</summary>
    public static readonly string[] ActivityKinds =
        ["quest", "mini_game", "competition", "event", "survey"];

    /// <summary>Допустимые типы вопросов анкеты: совпадают с <c>survey_questions.question_type</c>.</summary>
    public static readonly string[] SurveyQuestionKinds =
        ["text", "single_choice", "multiple_choice", "number", "boolean", "rating"];

    private static readonly Regex FenceMarkers = new("```[a-zA-Z]*", RegexOptions.Compiled);
    private static readonly Regex HtmlTags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new("[\\t ]{2,}", RegexOptions.Compiled);
    private static readonly Regex BlankLines = new("\\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Очищает текст: убирает markdown-обёртку и HTML, схлопывает пробелы,
    /// обрезает по длине и возвращает <c>null</c> для пустого значения.
    /// </summary>
    /// <param name="value">Исходный текст.</param>
    /// <param name="maxLength">Максимальная длина.</param>
    /// <returns>Очищенный текст или <c>null</c>.</returns>
    public static string? Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = FenceMarkers.Replace(value, " ");
        text = HtmlTags.Replace(text, " ");
        text = text.Replace('\u00A0', ' ');
        text = Spaces.Replace(text, " ");
        text = BlankLines.Replace(text, "\n\n");
        text = text.Trim();

        if (text.Length == 0)
        {
            return null;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(1, maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// Нормализует тип выполнения шага квеста.
    /// </summary>
    /// <param name="value">Значение из ответа модели.</param>
    /// <returns><c>manual</c>, <c>proof</c> или <c>automatic</c>.</returns>
    public static string CompletionType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "proof" => "proof",
            "automatic" => "automatic",
            _ => "manual"
        };
    }

    /// <summary>
    /// Нормализует формат идеи активности.
    /// </summary>
    /// <param name="value">Значение из ответа модели.</param>
    /// <returns>Один из <see cref="ActivityKinds"/>; по умолчанию <c>event</c>.</returns>
    public static string ActivityKind(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized is not null && ActivityKinds.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : "event";
    }

    /// <summary>
    /// Нормализует трудоёмкость идеи.
    /// </summary>
    /// <param name="value">Значение из ответа модели.</param>
    /// <returns><c>low</c>, <c>medium</c> или <c>high</c>.</returns>
    public static string Effort(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "low" => "low",
            "high" => "high",
            _ => "medium"
        };
    }

    /// <summary>
    /// Нормализует тип вопроса анкеты.
    /// </summary>
    /// <param name="value">Значение из ответа модели.</param>
    /// <returns>Один из <see cref="SurveyQuestionKinds"/>; по умолчанию <c>text</c>.</returns>
    public static string SurveyQuestionType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized is not null && SurveyQuestionKinds.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : "text";
    }

    /// <summary>
    /// Очищает список строк: удаляет пустые и повторы, ограничивает количество и длину.
    /// </summary>
    /// <param name="values">Исходные значения.</param>
    /// <param name="maxItems">Максимальное количество.</param>
    /// <param name="maxLength">Максимальная длина элемента.</param>
    /// <returns>Очищенный список.</returns>
    public static IReadOnlyList<string> StringList(
        IEnumerable<string?>? values,
        int maxItems,
        int maxLength)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values ?? [])
        {
            var text = Text(value, maxLength);

            if (text is null || !seen.Add(text))
            {
                continue;
            }

            result.Add(text);

            if (result.Count >= maxItems)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Проверяет и нормализует варианты квеста.</summary>
    /// <param name="items">Варианты из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные варианты.</returns>
    public static IReadOnlyList<QuestDraft> QuestDrafts(
        IEnumerable<QuestDraftPayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxVariants);
        var result = new List<QuestDraft>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var name = Text(item.Name, TitleMaxLength);
            var description = Text(item.Description, DescriptionMaxLength);

            if (name is null || description is null || !seen.Add(name))
            {
                continue;
            }

            result.Add(new QuestDraft(name, description));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет и нормализует шаги квеста. Автоматические шаги разрешаются только
    /// с кодами действий и идентификаторами из списков, переданных Backend-ом:
    /// выдуманный идентификатор отбрасывается, а шаг помечается как требующий настройки.
    /// </summary>
    /// <param name="items">Шаги из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <param name="candidates">Реальные объекты, переданные модели.</param>
    /// <param name="allowAutomatic">Разрешены ли автоматические шаги.</param>
    /// <returns>Пригодные шаги.</returns>
    public static IReadOnlyList<QuestStepDraft> QuestSteps(
        IEnumerable<QuestStepPayload>? items,
        int max,
        QuestStepActionCandidateSets? candidates = null,
        bool allowAutomatic = true)
    {
        var limit = Math.Clamp(max, 1, MaxSteps);
        var result = new List<QuestStepDraft>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var title = Text(item.Title, TitleMaxLength);
            var description = Text(item.Description, DescriptionMaxLength);

            if (title is null || description is null || !seen.Add(title))
            {
                continue;
            }

            var completionType = CompletionType(item.CompletionType);
            string? actionType = null;
            long? targetId = null;
            string? achievementCode = null;
            var needsConfiguration = false;

            if (completionType == "automatic")
            {
                if (!allowAutomatic
                    || !TryResolveAction(
                        item,
                        candidates,
                        out actionType,
                        out targetId,
                        out achievementCode,
                        out needsConfiguration))
                {
                    // Неизвестное или недоступное действие не пробрасывается в админку:
                    // шаг становится ручным.
                    completionType = "manual";
                    actionType = null;
                    targetId = null;
                    achievementCode = null;
                    needsConfiguration = false;
                }
            }

            result.Add(new QuestStepDraft(
                title,
                description,
                completionType,
                item.IsRequired,
                actionType,
                targetId,
                achievementCode,
                needsConfiguration));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    private static bool TryResolveAction(
        QuestStepPayload item,
        QuestStepActionCandidateSets? candidates,
        out string? actionType,
        out long? targetId,
        out string? achievementCode,
        out bool needsConfiguration)
    {
        actionType = null;
        targetId = null;
        achievementCode = null;
        needsConfiguration = false;

        var template = QuestStepActionRegistry.Find(item.ActionType);

        if (template is null || !template.IsAvailable)
        {
            return false;
        }

        actionType = template.Code;

        var requiresTarget = template.Parameters.Any(parameter =>
            string.Equals(parameter.Name, "targetId", StringComparison.Ordinal));

        if (requiresTarget)
        {
            var allowed = AllowedTargets(template.Code, candidates);

            if (item.TargetId is long id && allowed.Contains(id))
            {
                targetId = id;
            }
            else
            {
                needsConfiguration = true;
            }
        }

        if (template.Code == QuestStepActionCodes.EarnAchievement)
        {
            var allowed = candidates is null
                ? []
                : candidates.Achievements
                    .Select(option => option.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var code = item.AchievementCode?.Trim();

            if (!string.IsNullOrWhiteSpace(code) && allowed.Contains(code))
            {
                achievementCode = code;
            }
            else
            {
                needsConfiguration = true;
            }
        }

        return true;
    }

    private static HashSet<long> AllowedTargets(
        string code,
        QuestStepActionCandidateSets? candidates)
    {
        var options = code switch
        {
            QuestStepActionCodes.CompleteQuest => candidates?.Quests,
            QuestStepActionCodes.CompleteSurvey => candidates?.Surveys,
            QuestStepActionCodes.AttendEvent => candidates?.Events,
            _ => null
        };

        if (options is null)
        {
            return [];
        }

        var allowed = new HashSet<long>();

        foreach (var option in options)
        {
            if (long.TryParse(option.Id, out var id) && id > 0)
            {
                allowed.Add(id);
            }
        }

        return allowed;
    }

    /// <summary>Проверяет и нормализует анонсы мероприятий.</summary>
    /// <param name="items">Анонсы из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные анонсы.</returns>
    public static IReadOnlyList<EventAnnouncement> EventAnnouncements(
        IEnumerable<EventAnnouncementPayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxVariants);
        var result = new List<EventAnnouncement>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var title = Text(item.Title, TitleMaxLength);
            var summary = Text(item.Summary, SummaryMaxLength);
            var body = Text(item.Body, BodyMaxLength);

            if (title is null || summary is null || body is null || !seen.Add(title))
            {
                continue;
            }

            result.Add(new EventAnnouncement(
                title,
                summary,
                body,
                Text(item.CallToAction, CallToActionMaxLength)));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Проверяет и нормализует описания наград.</summary>
    /// <param name="items">Описания из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные описания.</returns>
    public static IReadOnlyList<RewardDescription> RewardDescriptions(
        IEnumerable<RewardDescriptionPayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxVariants);
        var result = new List<RewardDescription>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var name = Text(item.Name, TitleMaxLength);
            var description = Text(item.Description, DescriptionMaxLength);

            if (name is null || description is null || !seen.Add(name))
            {
                continue;
            }

            var pitch = Text(item.ShortPitch, ShortPitchMaxLength)
                ?? Text(description, ShortPitchMaxLength)
                ?? description;

            result.Add(new RewardDescription(name, description, pitch));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>Проверяет и нормализует варианты анкеты.</summary>
    /// <param name="items">Варианты из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные варианты.</returns>
    public static IReadOnlyList<SurveyDraft> SurveyDrafts(
        IEnumerable<SurveyDraftPayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxVariants);
        var result = new List<SurveyDraft>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var title = Text(item.Title, TitleMaxLength);
            var description = Text(item.Description, DescriptionMaxLength);

            if (title is null || description is null || !seen.Add(title))
            {
                continue;
            }

            result.Add(new SurveyDraft(title, description));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет и нормализует вопросы анкеты. Тип вопроса приводится к поддерживаемому,
    /// варианты ответа остаются только у типов-выборов, вопрос-выбор без двух вариантов
    /// превращается в свободный текст, чтобы администратор не получил нерабочий вопрос.
    /// </summary>
    /// <param name="items">Вопросы из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные вопросы.</returns>
    public static IReadOnlyList<SurveyQuestionDraft> SurveyQuestions(
        IEnumerable<SurveyQuestionPayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxSurveyQuestions);
        var result = new List<SurveyQuestionDraft>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var questionText = Text(item.QuestionText, QuestionTextMaxLength);

            if (questionText is null || !seen.Add(questionText))
            {
                continue;
            }

            var questionType = SurveyQuestionType(item.QuestionType);
            var options = Options(item.Options);

            if (IsChoice(questionType) && options.Count < 2)
            {
                questionType = "text";
                options = [];
            }

            if (!IsChoice(questionType))
            {
                options = [];
            }

            result.Add(new SurveyQuestionDraft(
                questionText,
                questionType,
                options,
                item.IsRequired));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Очищает варианты ответа: убирает пустые и повторы, ограничивает количество и длину.
    /// </summary>
    /// <param name="values">Варианты из ответа модели.</param>
    /// <returns>Очищенные варианты.</returns>
    private static IReadOnlyList<string> Options(IEnumerable<string?>? values) =>
        StringList(values, MaxQuestionOptions, OptionMaxLength);

    private static bool IsChoice(string questionType) =>
        questionType is "single_choice" or "multiple_choice";

    /// <summary>Проверяет и нормализует улучшенный текст.</summary>
    /// <param name="payload">Ответ модели.</param>
    /// <returns>Улучшенный текст или <c>null</c>, если текст непригоден.</returns>
    public static ImprovedText? ImprovedText(ImprovedTextPayload? payload)
    {
        var text = Text(payload?.Text, ImprovedTextMaxLength);

        if (text is null)
        {
            return null;
        }

        return new ImprovedText(
            text,
            StringList(payload?.Changes, ChangesMaxItems, ChangeMaxLength));
    }

    /// <summary>Проверяет и нормализует альтернативные версии текста.</summary>
    /// <param name="items">Варианты из ответа модели.</param>
    /// <param name="max">Максимальное количество.</param>
    /// <returns>Пригодные варианты.</returns>
    public static IReadOnlyList<TextAlternative> Alternatives(
        IEnumerable<TextAlternativePayload>? items,
        int max)
    {
        var limit = Math.Clamp(max, 1, MaxVariants);
        var result = new List<TextAlternative>(limit);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var text = Text(item.Text, ImprovedTextMaxLength);

            if (text is null || !seen.Add(text))
            {
                continue;
            }

            var label = Text(item.Label, TitleMaxLength) ?? $"Вариант {result.Count + 1}";

            result.Add(new TextAlternative(label, text));

            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }
}
