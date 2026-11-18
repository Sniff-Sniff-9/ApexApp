namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>Транспортная модель ответа: варианты названия и описания квеста.</summary>
public sealed class QuestDraftsPayload
{
    /// <summary>Варианты.</summary>
    public List<QuestDraftPayload>? Variants { get; set; }
}

/// <summary>Транспортная модель одного варианта квеста.</summary>
public sealed class QuestDraftPayload
{
    /// <summary>Название.</summary>
    public string? Name { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }
}

/// <summary>Транспортная модель ответа: шаги квеста.</summary>
public sealed class QuestStepsPayload
{
    /// <summary>Шаги.</summary>
    public List<QuestStepPayload>? Steps { get; set; }
}

/// <summary>Транспортная модель одного шага квеста.</summary>
public sealed class QuestStepPayload
{
    /// <summary>Название шага.</summary>
    public string? Title { get; set; }

    /// <summary>Описание шага.</summary>
    public string? Description { get; set; }

    /// <summary>Тип выполнения: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</summary>
    public string? CompletionType { get; set; }

    /// <summary>Код автоматического действия или <c>null</c>.</summary>
    public string? ActionType { get; set; }

    /// <summary>Идентификатор выбранного объекта из списка Backend-а.</summary>
    public long? TargetId { get; set; }

    /// <summary>Код достижения из списка Backend-а.</summary>
    public string? AchievementCode { get; set; }

    /// <summary>Признак обязательного шага.</summary>
    public bool IsRequired { get; set; }
}

/// <summary>Транспортная модель ответа: варианты названия и описания анкеты.</summary>
public sealed class SurveyDraftsPayload
{
    /// <summary>Варианты.</summary>
    public List<SurveyDraftPayload>? Variants { get; set; }
}

/// <summary>Транспортная модель одного варианта анкеты.</summary>
public sealed class SurveyDraftPayload
{
    /// <summary>Название анкеты.</summary>
    public string? Title { get; set; }

    /// <summary>Описание анкеты.</summary>
    public string? Description { get; set; }
}

/// <summary>Транспортная модель ответа: вопросы анкеты.</summary>
public sealed class SurveyQuestionsPayload
{
    /// <summary>Вопросы.</summary>
    public List<SurveyQuestionPayload>? Questions { get; set; }
}

/// <summary>Транспортная модель одного вопроса анкеты.</summary>
public sealed class SurveyQuestionPayload
{
    /// <summary>Текст вопроса.</summary>
    public string? QuestionText { get; set; }

    /// <summary>
    /// Тип вопроса: <c>text</c>, <c>single_choice</c>, <c>multiple_choice</c>,
    /// <c>number</c>, <c>boolean</c> или <c>rating</c>.
    /// </summary>
    public string? QuestionType { get; set; }

    /// <summary>Варианты ответа для типов-выборов.</summary>
    public List<string>? Options { get; set; }

    /// <summary>Признак обязательного вопроса.</summary>
    public bool IsRequired { get; set; }
}

/// <summary>Транспортная модель ответа: анонсы мероприятий.</summary>
public sealed class EventAnnouncementsPayload
{
    /// <summary>Варианты анонса.</summary>
    public List<EventAnnouncementPayload>? Variants { get; set; }
}

/// <summary>Транспортная модель одного анонса.</summary>
public sealed class EventAnnouncementPayload
{
    /// <summary>Заголовок.</summary>
    public string? Title { get; set; }

    /// <summary>Короткое описание.</summary>
    public string? Summary { get; set; }

    /// <summary>Полный текст.</summary>
    public string? Body { get; set; }

    /// <summary>Призыв к действию.</summary>
    public string? CallToAction { get; set; }
}

/// <summary>Транспортная модель ответа: описания награды.</summary>
public sealed class RewardDescriptionsPayload
{
    /// <summary>Варианты описания.</summary>
    public List<RewardDescriptionPayload>? Variants { get; set; }
}

/// <summary>Транспортная модель одного описания награды.</summary>
public sealed class RewardDescriptionPayload
{
    /// <summary>Название награды.</summary>
    public string? Name { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Короткий питч.</summary>
    public string? ShortPitch { get; set; }
}

/// <summary>Транспортная модель ответа: улучшенный текст.</summary>
public sealed class ImprovedTextPayload
{
    /// <summary>Улучшенный текст.</summary>
    public string? Text { get; set; }

    /// <summary>Что изменено.</summary>
    public List<string>? Changes { get; set; }
}

/// <summary>Транспортная модель ответа: альтернативные версии текста.</summary>
public sealed class TextAlternativesPayload
{
    /// <summary>Варианты.</summary>
    public List<TextAlternativePayload>? Variants { get; set; }
}

/// <summary>Транспортная модель одной альтернативной версии.</summary>
public sealed class TextAlternativePayload
{
    /// <summary>Короткая подпись варианта.</summary>
    public string? Label { get; set; }

    /// <summary>Текст варианта.</summary>
    public string? Text { get; set; }
}

/// <summary>Транспортная модель ответа: рекомендации по вовлечённости.</summary>
public sealed class EngagementRecommendationsPayload
{
    /// <summary>Краткий анализ.</summary>
    public string? Analysis { get; set; }

    /// <summary>Идеи активностей.</summary>
    public List<ActivityIdeaPayload>? ActivityIdeas { get; set; }

    /// <summary>Идеи наград.</summary>
    public List<RewardIdeaPayload>? RewardIdeas { get; set; }

    /// <summary>Ограничения данных, о которых предупреждает модель.</summary>
    public List<string>? DataLimitations { get; set; }
}

/// <summary>Транспортная модель идеи активности.</summary>
public sealed class ActivityIdeaPayload
{
    /// <summary>Формат: <c>quest</c>, <c>mini_game</c>, <c>competition</c>, <c>event</c> или <c>survey</c>.</summary>
    public string? Kind { get; set; }

    /// <summary>Название идеи.</summary>
    public string? Title { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Почему идея подходит данным.</summary>
    public string? WhyItFits { get; set; }

    /// <summary>Трудоёмкость: <c>low</c>, <c>medium</c> или <c>high</c>.</summary>
    public string? Effort { get; set; }

    /// <summary>Ожидаемый эффект.</summary>
    public string? ExpectedImpact { get; set; }
}

/// <summary>Транспортная модель идеи награды.</summary>
public sealed class RewardIdeaPayload
{
    /// <summary>Название.</summary>
    public string? Title { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Обоснование по данным.</summary>
    public string? Rationale { get; set; }

    /// <summary>Ориентировочный диапазон цены в баллах.</summary>
    public string? SuggestedPriceBand { get; set; }
}

/// <summary>Транспортная модель ответа: рекомендации по каталогу наград.</summary>
public sealed class ProductRecommendationsPayload
{
    /// <summary>Краткий анализ спроса.</summary>
    public string? Analysis { get; set; }

    /// <summary>Идеи товаров.</summary>
    public List<ProductIdeaPayload>? ProductIdeas { get; set; }

    /// <summary>Действия по каталогу.</summary>
    public List<string>? CatalogActions { get; set; }

    /// <summary>Ограничения данных, о которых предупреждает модель.</summary>
    public List<string>? DataLimitations { get; set; }
}

/// <summary>Транспортная модель идеи товара.</summary>
public sealed class ProductIdeaPayload
{
    /// <summary>Название.</summary>
    public string? Name { get; set; }

    /// <summary>Категория.</summary>
    public string? Category { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Короткий питч.</summary>
    public string? ShortPitch { get; set; }

    /// <summary>Ориентировочный диапазон цены в баллах.</summary>
    public string? PriceBand { get; set; }

    /// <summary>Обоснование по данным.</summary>
    public string? Rationale { get; set; }

    /// <summary>Теги.</summary>
    public List<string>? Tags { get; set; }
}
