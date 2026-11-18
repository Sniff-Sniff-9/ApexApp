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

    /// <summary>Товар для <c>make_purchase</c> из списка Backend-а.</summary>
    public long? ProductId { get; set; }

    /// <summary>Минимальная сумма заказа для <c>make_purchase</c>.</summary>
    public long? MinAmount { get; set; }

    /// <summary>Количество благодарностей для <c>send_thanks</c>/<c>receive_thanks</c>.</summary>
    public int? Count { get; set; }

    /// <summary>Требование аватара для <c>complete_profile</c>.</summary>
    public bool? RequireAvatar { get; set; }

    /// <summary>Признак обязательного шага.</summary>
    public bool IsRequired { get; set; }
}

/// <summary>Транспортная модель ответа: полный черновик квеста.</summary>
public sealed class QuestActivityDraftPayload
{
    /// <summary>Название.</summary>
    public string? Title { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Шаги.</summary>
    public List<QuestStepPayload>? Steps { get; set; }
}

/// <summary>Транспортная модель ответа: полный черновик мероприятия.</summary>
public sealed class EventActivityDraftPayload
{
    /// <summary>Название.</summary>
    public string? Title { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }
}

/// <summary>Транспортная модель ответа: полный черновик анкеты.</summary>
public sealed class SurveyActivityDraftPayload
{
    /// <summary>Название.</summary>
    public string? Title { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Вопросы.</summary>
    public List<SurveyQuestionPayload>? Questions { get; set; }
}

/// <summary>Транспортная модель ответа: полный черновик товара.</summary>
public sealed class ProductDraftPayload
{
    /// <summary>Название.</summary>
    public string? Name { get; set; }

    /// <summary>Описание.</summary>
    public string? Description { get; set; }

    /// <summary>Цена в баллах, предложенная моделью.</summary>
    public long? Price { get; set; }

    /// <summary>Категория из списка Backend-а.</summary>
    public int? CategoryId { get; set; }
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

    /// <summary>Рекомендации существующих активностей.</summary>
    public List<ActivityRecommendationPayload>? RecommendedActivities { get; set; }

    /// <summary>Ограничения данных, о которых предупреждает модель.</summary>
    public List<string>? DataLimitations { get; set; }
}

/// <summary>Транспортная модель рекомендации существующей активности.</summary>
public sealed class ActivityRecommendationPayload
{
    /// <summary>Формат: <c>event</c>, <c>quest</c> или <c>survey</c>.</summary>
    public string? Format { get; set; }

    /// <summary>Идентификатор существующей активности.</summary>
    public long? ActivityId { get; set; }

    /// <summary>Рекомендуемое действие.</summary>
    public string? Action { get; set; }

    /// <summary>Обоснование по реальным показателям.</summary>
    public string? Rationale { get; set; }

    /// <summary>Ожидаемый эффект.</summary>
    public string? ExpectedImpact { get; set; }
}

/// <summary>Транспортная модель ответа: рекомендации по каталогу наград.</summary>
public sealed class ProductRecommendationsPayload
{
    /// <summary>Краткий анализ спроса.</summary>
    public string? Analysis { get; set; }

    /// <summary>Рекомендации существующих товаров.</summary>
    public List<ProductRecommendationPayload>? RecommendedProducts { get; set; }

    /// <summary>Ограничения данных, о которых предупреждает модель.</summary>
    public List<string>? DataLimitations { get; set; }
}

/// <summary>Транспортная модель рекомендации существующего товара.</summary>
public sealed class ProductRecommendationPayload
{
    /// <summary>Идентификатор существующего товара.</summary>
    public int? ProductId { get; set; }

    /// <summary>Рекомендуемое действие.</summary>
    public string? Action { get; set; }

    /// <summary>Обоснование по реальным показателям.</summary>
    public string? Rationale { get; set; }

    /// <summary>Ожидаемый эффект.</summary>
    public string? ExpectedImpact { get; set; }
}
