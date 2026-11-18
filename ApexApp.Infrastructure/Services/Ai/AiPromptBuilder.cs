using System.Text;
using ApexApp.Infrastructure.Services.Quests;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Собирает пользовательские промпты для всех сценариев модуля. Метод чистый и
/// детерминированный: одинаковый запрос дает одинаковый текст, поэтому сборку
/// можно проверять без обращения к провайдеру.
/// </summary>
public static class AiPromptBuilder
{
    private const string NotSpecified = "не задано";
    private const int MaxInputLength = 6000;

    /// <summary>Промпт для генерации названия и описания квеста.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string QuestDrafts(AiQuestDraftRequest request)
    {
        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);

        return $$"""
            Задача: предложи {{variants}} варианта названия и описания нового квеста для сотрудников.

            Параметры:
            <input>
            тема: {{Clean(request.Topic)}}
            аудитория: {{Clean(request.Audience)}}
            награда за прохождение: {{Points(request.RewardAmount)}}
            длительность: {{Days(request.DurationDays)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - название: 3–8 слов, до 80 символов, без кавычек и точки в конце;
            - описание: 300–900 символов; объясняет, зачем сотруднику проходить квест и что его ждет;
            - варианты должны отличаться подходом, а не перестановкой слов;
            - не обещай призы, сроки и условия, которых нет в параметрах.

            Верни только json:
            {"variants":[{"name":"Название квеста","description":"Описание квеста"}]}
            """;
    }

    /// <summary>Промпт для генерации шагов квеста.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="candidates">Реальные объекты из базы, допустимые для автоматических шагов.</param>
    /// <param name="allowAutomatic">Разрешены ли автоматические шаги.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string QuestSteps(
        AiQuestStepRequest request,
        QuestStepActionCandidateSets? candidates = null,
        bool allowAutomatic = true)
    {
        var steps = Math.Clamp(request.StepsCount, 1, AiContentValidator.MaxSteps);
        var options = BuildQuestStepOptions(candidates, allowAutomatic);

        return $$"""
            Задача: составь {{steps}} шагов квеста «{{Clean(request.QuestTitle)}}».

            Контекст:
            <input>
            описание квеста: {{Clean(request.QuestDescription)}}
            подсказка о доказательствах: {{Clean(request.ProofHint)}}
            </input>

            {{options}}

            Требования:
            - каждый шаг — конкретное действие, выполнимое за один–три дня;
            - первый шаг не должен требовать загрузки файлов;
            - completionType: "manual" (отметка о выполнении), "proof" (нужен файл или фото),
              "automatic" (система проверяет сама);
            - actionType указывай только для automatic и только из списка действий выше;
            - targetId, achievementCode и productId выбирай только из списков объектов выше;
            - для make_purchase выбери productId, minAmount, оба условия или ни одного (любая покупка);
            - count — целое число не меньше 1 (без верхнего предела); minAmount — не меньше 0;
            - requireAvatar допустим только true; complete_profile проверяет только аватар;
            - никогда не придумывай идентификаторы и не используй объекты вне списков;
            - если для automatic не подходит ни один объект из списков, верни manual шаг;
            - если автоматическое действие уместно, но подходящего объекта нет, верни
              actionType, targetId null, achievementCode null и needsConfiguration true;
            - хотя бы один шаг обязателен;
            - title до 80 символов, description 150–500 символов.

            Верни только json:
            {"steps":[{"title":"Название шага","description":"Описание шага","completionType":"manual","actionType":null,"targetId":null,"achievementCode":null,"productId":null,"minAmount":null,"count":null,"requireAvatar":null,"needsConfiguration":false,"isRequired":true}]}
            """;
    }

    private static string BuildQuestStepOptions(
        QuestStepActionCandidateSets? candidates,
        bool allowAutomatic)
    {
        if (!allowAutomatic)
        {
            return "Автоматические шаги запрещены: используй только \"manual\" или \"proof\".";
        }

        var builder = new StringBuilder();

        builder.AppendLine("Доступные автоматические действия (actionType):");

        foreach (var template in QuestStepActionRegistry.All.Where(template => template.IsAvailable))
        {
            builder.AppendLine($"- {template.Code}: {template.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("Доступные объекты (только эти идентификаторы):");
        AppendOptions(builder, "quests", "квесты", candidates?.Quests);
        AppendOptions(builder, "surveys", "анкеты", candidates?.Surveys);
        AppendOptions(builder, "events", "мероприятия", candidates?.Events);
        AppendOptions(builder, "achievements", "достижения", candidates?.Achievements);
        AppendOptions(builder, "products", "товары", candidates?.Products);

        return builder.ToString().Trim();
    }

    private static void AppendOptions(
        StringBuilder builder,
        string tag,
        string label,
        IReadOnlyList<QuestStepActionOption>? options)
    {
        builder.AppendLine($"<{tag}>");
        builder.AppendLine(label + ":");

        if (options is null || options.Count == 0)
        {
            builder.AppendLine("нет доступных объектов");
        }
        else
        {
            foreach (var option in options)
            {
                var subtitle = string.IsNullOrWhiteSpace(option.Subtitle)
                    ? string.Empty
                    : $" ({option.Subtitle})";

                builder.AppendLine($"{option.Id}: {Clean(option.Title)}{subtitle}");
            }
        }

        builder.AppendLine($"</{tag}>");
    }

    /// <summary>Промпт для генерации анонса мероприятия.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string EventAnnouncement(AiEventAnnouncementRequest request)
    {
        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);

        return $$"""
            Задача: напиши {{variants}} варианта анонса корпоративного мероприятия.

            Параметры:
            <input>
            рабочее название: {{Clean(request.Title)}}
            место: {{Clean(request.Location)}}
            начало: {{Moment(request.StartAt)}}
            награда за посещение: {{Points(request.RewardAmount)}}
            лимит участников: {{Count(request.MaxParticipants)}}
            аудитория: {{Clean(request.Audience)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - title: до 80 символов;
            - summary: одно–два предложения, до 300 символов;
            - body: 600–1200 символов; что будет, кому полезно, как подготовиться;
            - callToAction: короткий призыв, до 120 символов;
            - не выдумывай программу, спикеров и условия, которых нет в параметрах.

            Верни только json:
            {"variants":[{"title":"Заголовок","summary":"Короткое описание","body":"Полный текст","callToAction":"Призыв"}]}
            """;
    }

    /// <summary>Промпт для генерации названия и описания награды.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string RewardDescription(AiRewardDescriptionRequest request)
    {
        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);

        return $$"""
            Задача: придумай {{variants}} варианта названия и описания награды для каталога.

            Параметры:
            <input>
            предмет: {{Clean(request.ItemName)}}
            категория: {{Clean(request.Category)}}
            цена в баллах: {{Points(request.Price)}}
            аудитория: {{Clean(request.Audience)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - name: до 80 символов, без кавычек;
            - description: 200–600 символов; чем полезна награда и кому она подойдет;
            - shortPitch: одна фраза до 200 символов для карточки каталога;
            - не указывай бренды, характеристики и цену, которых нет в параметрах.

            Верни только json:
            {"variants":[{"name":"Название","description":"Описание","shortPitch":"Короткая фраза"}]}
            """;
    }

    /// <summary>Промпт для генерации названия и описания анкеты.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string SurveyDraft(AiSurveyDraftRequest request)
    {
        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);

        return $$"""
            Задача: предложи {{variants}} варианта названия и описания новой анкеты (опроса) для сотрудников.

            Параметры:
            <input>
            тема или цель: {{Clean(request.Topic)}}
            аудитория: {{Clean(request.Audience)}}
            награда за прохождение: {{Points(request.RewardAmount)}}
            вопросов в анкете: {{Questions(request.QuestionsCount)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - title: 3–8 слов, до 80 символов, без кавычек и точки в конце;
            - description: 200–600 символов; кому и зачем проходить анкету и что будет с результатами;
            - варианты должны отличаться подходом, а не перестановкой слов;
            - не обещай призы, сроки и условия, которых нет в параметрах;
            - не подсказывай «удобный» ответ и не обещай анонимность, если её нет в параметрах.

            Верни только json:
            {"variants":[{"title":"Название анкеты","description":"Описание анкеты"}]}
            """;
    }

    /// <summary>Промпт для генерации вопросов анкеты.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string SurveyQuestions(AiSurveyQuestionsRequest request)
    {
        var questions = Math.Clamp(
            request.QuestionsCount,
            1,
            AiContentValidator.MaxSurveyQuestions);

        return $$"""
            Задача: составь {{questions}} вопросов анкеты «{{Clean(request.SurveyTitle)}}».

            Контекст:
            <input>
            описание анкеты: {{Clean(request.SurveyDescription)}}
            аудитория: {{Clean(request.Audience)}}
            </input>

            Требования:
            - questionText: одно предложение, 20–300 символов, без нумерации и разметки;
            - questionType: "text", "single_choice", "multiple_choice", "number",
              "boolean" или "rating";
            - options: 2–6 вариантов только для "single_choice" и "multiple_choice";
              для остальных типов верни пустой массив;
            - isRequired: true у ключевых вопросов; необязательных вопросов — не больше двух;
            - вопросы не дублируют друг друга и не подсказывают «удобный» ответ;
            - смешивай типы: не делай все вопросы одинаковыми.

            Верни только json:
            {"questions":[{"questionText":"Текст вопроса","questionType":"single_choice","options":["Вариант 1","Вариант 2"],"isRequired":true}]}
            """;
    }

    /// <summary>Промпт для полного черновика квеста.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="candidates">Реальные объекты для автоматических шагов.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string QuestActivityDraft(
        AiQuestActivityDraftRequest request,
        QuestStepActionCandidateSets? candidates)
    {
        var steps = Math.Clamp(request.StepsCount, 1, AiContentValidator.MaxSteps);
        var options = BuildQuestStepOptions(candidates, request.AllowAutomatic);

        return $$"""
            Задача: подготовь полный черновик квеста для сотрудников по свободному запросу администратора.

            Запрос администратора:
            <input>
            {{Clean(request.Prompt)}}
            аудитория: {{Clean(request.Audience)}}
            тон: {{Clean(request.Tone)}}
            </input>

            {{options}}

            Требования:
            - title: 3–8 слов, до 80 символов, без кавычек и точки в конце;
            - description: 300–900 символов; зачем сотруднику проходить квест и что его ждёт;
            - steps: ровно {{steps}} шагов;
            - completionType: "manual", "proof" или "automatic";
            - для automatic обязательно заполни actionType и параметры из списка выше;
            - targetId, achievementCode и productId выбирай только из списков объектов выше;
            - count — целое число не меньше 1; minAmount — целое число не меньше 0;
            - никогда не придумывай идентификаторы и не используй объекты вне списков;
            - даты, награду и признак активности не возвращай: их задаёт форма;
            - не обещай призы, сроки и условия, которых нет в запросе;
            - хотя бы один шаг обязателен; отметь обязательные шаги isRequired=true.

            Верни только json:
            {"title":"Название квеста","description":"Описание квеста","steps":[{"title":"Шаг","description":"Описание шага","completionType":"manual","actionType":null,"targetId":null,"achievementCode":null,"productId":null,"minAmount":null,"count":null,"requireAvatar":null,"isRequired":true}]}
            """;
    }

    /// <summary>Промпт для полного черновика мероприятия.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string EventActivityDraft(AiEventActivityDraftRequest request)
    {
        return $$"""
            Задача: подготовь полный черновик корпоративного мероприятия по свободному запросу администратора.

            Запрос администратора:
            <input>
            {{Clean(request.Prompt)}}
            аудитория: {{Clean(request.Audience)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - title: до 80 символов;
            - description: 300–900 символов; что будет, кому полезно, как подготовиться;
            - не выдумывай программу, спикеров, место и условия;
            - даты, место, лимит участников, награду и активность не возвращай: их задаёт форма.

            Верни только json:
            {"title":"Название мероприятия","description":"Описание мероприятия"}
            """;
    }

    /// <summary>Промпт для полного черновика анкеты.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string SurveyActivityDraft(AiSurveyActivityDraftRequest request)
    {
        var questions = Math.Clamp(
            request.QuestionsCount,
            1,
            AiContentValidator.MaxSurveyQuestions);

        return $$"""
            Задача: подготовь полный черновик анкеты для сотрудников по свободному запросу администратора.

            Запрос администратора:
            <input>
            {{Clean(request.Prompt)}}
            аудитория: {{Clean(request.Audience)}}
            тон: {{Clean(request.Tone)}}
            </input>

            Требования:
            - title: 3–8 слов, до 80 символов, без кавычек и точки в конце;
            - description: 200–600 символов; кому и зачем проходить анкету;
            - questions: ровно {{questions}} вопросов;
            - questionType: "text", "single_choice", "multiple_choice", "number", "boolean" или "rating";
            - options: 2–6 вариантов только для choice-типов; иначе пустой массив;
            - isRequired: true у ключевых вопросов; необязательных — не больше двух;
            - вопросы не дублируют друг друга и не подсказывают «удобный» ответ;
            - даты, награду, повторные ответы и активность не возвращай: их задаёт форма.

            Верни только json:
            {"title":"Название анкеты","description":"Описание анкеты","questions":[{"questionText":"Текст вопроса","questionType":"single_choice","options":["Вариант 1","Вариант 2"],"isRequired":true}]}
            """;
    }

    /// <summary>Промпт для полного черновика товара.</summary>
    /// <param name="request">Параметры.</param>
    /// <param name="categories">Реальные категории каталога.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string ProductDraft(
        AiProductDraftRequest request,
        IReadOnlyList<CategoryOption> categories)
    {
        var categoryLines = categories.Count == 0
            ? "нет доступных категорий"
            : string.Join(
                Environment.NewLine,
                categories.Select(category => $"- {category.Id}: {Clean(category.Name)}"));

        return $$"""
            Задача: подготовь черновик товара для каталога наград по свободному запросу мерчандайзера.

            Запрос мерчандайзера:
            <input>
            {{Clean(request.Prompt)}}
            тон: {{Clean(request.Tone)}}
            цена из формы: {{Points(request.Price)}}
            остаток из формы: {{Stock(request.Stock)}}
            </input>

            Реальные категории (выбирай только этот id):
            <categories>
            {{categoryLines}}
            </categories>

            Требования:
            - name: 3–80 символов, без кавычек;
            - description: 200–600 символов; чем полезен товар и кому он подойдёт;
            - categoryId: только идентификатор из списка категорий или null;
            - price: если цена задана в форме, не меняй её; если нет — предложи цену в баллах;
            - не придумывай бренды, изображения, остатки и характеристики;
            - digital-товары запрещены: не предлагай цифровые товары.

            Верни только json:
            {"name":"Название","description":"Описание","categoryId":null,"price":500}
            """;
    }

    /// <summary>Промпт для улучшения существующего текста.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string Improve(AiImproveTextRequest request)
    {
        return $$"""
            Задача: улучши текст для корпоративной платформы.

            Тип текста: {{Clean(request.TextKind)}}
            Пожелание заказчика: {{Clean(request.Instruction)}}
            Тон: {{Clean(request.Tone)}}

            Исходный текст:
            <input>
            {{Clean(request.Text)}}
            </input>

            Требования:
            - сохрани смысл и факты, ничего не добавляй от себя;
            - убери канцелярит, повторы и лишние вводные слова;
            - если задан тип текста, соблюдай его формат и длину;
            - changes: 3–6 коротких пунктов по-русски, что именно изменено.

            Верни только json:
            {"text":"Улучшенный текст","changes":["Что изменено"]}
            """;
    }

    /// <summary>Промпт для генерации альтернативных версий текста.</summary>
    /// <param name="request">Параметры.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string Alternatives(AiAlternativesRequest request)
    {
        var variants = Math.Clamp(request.Variants, 1, AiContentValidator.MaxVariants);

        return $$"""
            Задача: предложи {{variants}} альтернативных версий текста.

            Тип текста: {{Clean(request.TextKind)}}
            Тон: {{Clean(request.Tone)}}

            Исходный текст:
            <input>
            {{Clean(request.Text)}}
            </input>

            Требования:
            - сохрани смысл и факты, ничего не добавляй от себя;
            - варианты должны отличаться структурой и формулировками, а не одним словом;
            - длина каждого варианта — в пределах плюс-минус 30% от исходной;
            - label: короткая подпись по-русски, чем этот вариант отличается.

            Верни только json:
            {"variants":[{"label":"Короче и строже","text":"Текст варианта"}]}
            """;
    }

    /// <summary>Промпт для рекомендаций по вовлеченности.</summary>
    /// <param name="digest">Сводка показателей, рассчитанная Backend-ом.</param>
    /// <param name="notes">Пожелания администратора.</param>
    /// <param name="maxActivities">Максимальное количество рекомендаций.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string EngagementRecommendations(
        string digest,
        string? notes,
        int maxActivities)
    {
        return $$"""
            Задача: проанализируй вовлеченность сотрудников и порекомендуй, что делать
            с уже существующими активностями.

            Показатели и существующие активности, рассчитанные Backend-ом:
            <statistics>
            {{digest}}
            </statistics>

            Пожелания заказчика:
            <input>
            {{Clean(notes)}}
            </input>

            Требования:
            - analysis: 2–5 предложений, только выводы из <statistics>;
            - recommendedActivities: не больше {{maxActivities}} записей;
            - format и activityId бери только из раздела «Существующие активности»;
            - не придумывай активности, названия, идентификаторы и метрики;
            - action: promote | repeat | extend | fix | retire | pause;
            - rationale обязательно содержит конкретное число или код наблюдения из <statistics>;
            - dataLimitations: ограничения выборки, о которых предупреждают сами данные.

            Верни только json:
            {"analysis":"Краткий анализ","recommendedActivities":[{"format":"quest","activityId":12,"action":"promote","rationale":"Охват 40.2% при 74 участниках","expectedImpact":"Ожидаемый эффект"}],"dataLimitations":["Ограничение"]}
            """;
    }

    /// <summary>Промпт для рекомендаций по каталогу наград.</summary>
    /// <param name="digest">Сводка спроса, рассчитанная Backend-ом.</param>
    /// <param name="notes">Пожелания мерчандайзера.</param>
    /// <param name="maxProducts">Максимальное количество рекомендаций.</param>
    /// <returns>Пользовательский промпт.</returns>
    public static string ProductRecommendations(
        string digest,
        string? notes,
        int maxProducts)
    {
        return $$"""
            Задача: проанализируй спрос в каталоге наград и порекомендуй действия
            с уже существующими товарами.

            Показатели и существующие товары, рассчитанные Backend-ом:
            <statistics>
            {{digest}}
            </statistics>

            Пожелания заказчика:
            <input>
            {{Clean(notes)}}
            </input>

            Требования:
            - analysis: 2–5 предложений, только выводы из <statistics>;
            - recommendedProducts: не больше {{maxProducts}} записей;
            - productId бери только из разделов с товарами выше;
            - не придумывай товары, названия, идентификаторы и метрики;
            - action: restock | reprice | promote | pause | bundle | replace;
            - rationale обязательно содержит конкретное число из <statistics>;
            - dataLimitations: ограничения выборки.

            Верни только json:
            {"analysis":"Краткий анализ","recommendedProducts":[{"productId":34,"action":"restock","rationale":"Остаток 3 при 12 заказанных единицах","expectedImpact":"Ожидаемый эффект"}],"dataLimitations":["Ограничение"]}
            """;
    }

    /// <summary>
    /// Очищает пользовательский ввод: убирает управляющие символы и угловые скобки,
    /// чтобы текст не мог закрыть тег <c>input</c> или <c>statistics</c>.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <returns>Безопасный текст для вставки в промпт.</returns>
    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NotSpecified;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var symbol in value.Trim())
        {
            if (symbol is '<' or '>')
            {
                continue;
            }

            if (char.IsControl(symbol) && symbol is not '\n' and not '\t')
            {
                continue;
            }

            builder.Append(symbol);
        }

        var text = builder.ToString().Trim();

        if (text.Length == 0)
        {
            return NotSpecified;
        }

        return text.Length <= MaxInputLength ? text : text[..MaxInputLength];
    }

    private static string Points(long? value) =>
        value is long amount && amount > 0 ? $"{amount} баллов" : NotSpecified;

    private static string Days(int? value) =>
        value is int days && days > 0 ? $"{days} дней" : NotSpecified;

    private static string Count(int? value) =>
        value is int count && count > 0 ? $"{count} человек" : NotSpecified;

    private static string Stock(int? value) =>
        value is int stock && stock >= 0 ? $"{stock} шт." : NotSpecified;

    private static string Questions(int? value) =>
        value is int count && count > 0 ? $"{count} вопросов" : NotSpecified;

    private static string Moment(DateTime? value) =>
        value is DateTime moment ? moment.ToString("yyyy-MM-dd HH:mm 'UTC'") : NotSpecified;
}
