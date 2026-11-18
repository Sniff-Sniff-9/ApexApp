using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexApp.Infrastructure.Services.Quests;

/// <summary>
/// Коды автоматических действий шага квеста. Значение хранится в
/// <c>quest_steps.completion_data -&gt;&gt; 'type'</c>; отдельный столбец или enum БД не нужен.
/// </summary>
public static class QuestStepActionCodes
{
    public const string CompleteQuest = "complete_quest";

    public const string CompleteSurvey = "complete_survey";

    public const string SendThanks = "send_thanks";

    public const string MakePurchase = "make_purchase";

    public const string AttendEvent = "attend_event";

    public const string CompleteProfile = "complete_profile";

    public const string ReceiveThanks = "receive_thanks";

    public const string EarnAchievement = "earn_achievement";

    public const string ReadCompanyNews = "read_company_news";
}

/// <summary>
/// Когда действие считается выполненным относительно момента присоединения к квесту.
/// </summary>
public enum QuestStepRetroactivityPolicy
{
    /// <summary>
    /// Действие должно произойти не раньше <c>quest_participations.started_at</c>.
    /// </summary>
    Window,

    /// <summary>
    /// Учитывается уже существующий устойчивый факт: пользователь не может повторить действие.
    /// </summary>
    State,

    /// <summary>
    /// Анкета: <see cref="Window"/>, если разрешены повторные ответы, иначе <see cref="State"/>.
    /// </summary>
    Survey
}

/// <summary>
/// Описание параметра шаблона для административной формы. <see cref="Kind"/> задаёт
/// элемент управления, <see cref="OptionsSource"/> — источник списка выбора.
/// </summary>
/// <param name="Name">Имя поля в конфигурации шага.</param>
/// <param name="Label">Подпись для администратора.</param>
/// <param name="Kind">Тип значения: <c>entity</c>, <c>number</c> или <c>boolean</c>.</param>
/// <param name="OptionsSource">Источник вариантов для <c>entity</c>.</param>
/// <param name="Required">Признак обязательного параметра.</param>
/// <param name="HelpText">Пояснение для администратора.</param>
public sealed record QuestStepActionParameter(
    string Name,
    string Label,
    string Kind,
    string? OptionsSource,
    bool Required,
    string? HelpText = null);

/// <summary>
/// Шаблон автоматического действия: понятное имя, описание, политика ретроактивности
/// и набор параметров. Список шаблонов — единственный источник истины для админки и ИИ.
/// </summary>
/// <param name="Code">Код действия.</param>
/// <param name="Name">Понятное название.</param>
/// <param name="Description">Понятное описание условия.</param>
/// <param name="Retroactivity">Политика ретроактивности.</param>
/// <param name="Parameters">Параметры шаблона.</param>
/// <param name="IsAvailable">Признак доступности шаблона.</param>
/// <param name="UnavailableReason">Причина недоступности.</param>
public sealed record QuestStepActionTemplate(
    string Code,
    string Name,
    string Description,
    QuestStepRetroactivityPolicy Retroactivity,
    IReadOnlyList<QuestStepActionParameter> Parameters,
    bool IsAvailable = true,
    string? UnavailableReason = null);

/// <summary>
/// Типизированная конфигурация автоматического шага. Сериализуется в существующий
/// <c>jsonb</c>-столбец <c>quest_steps.completion_data</c>; схема БД не меняется.
/// </summary>
/// <param name="Type">Код действия из <see cref="QuestStepActionCodes"/>.</param>
/// <param name="TargetId">Идентификатор выбранного объекта.</param>
/// <param name="AchievementCode">Код достижения для <c>earn_achievement</c>.</param>
/// <param name="ProductId">Товар для <c>make_purchase</c>.</param>
/// <param name="MinAmount">Минимальная сумма заказа для <c>make_purchase</c>.</param>
/// <param name="Count">Требуемое количество действий для благодарностей.</param>
/// <param name="RequireAvatar">Требование аватара для <c>complete_profile</c>.</param>
public sealed record QuestStepActionConfiguration(
    string Type,
    long? TargetId = null,
    string? AchievementCode = null,
    long? ProductId = null,
    long? MinAmount = null,
    int? Count = null,
    bool? RequireAvatar = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> PropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "targetId",
        "achievementCode",
        "productId",
        "minAmount",
        "count",
        "requireAvatar"
    };

    /// <summary>
    /// Сериализует конфигурацию для записи в <c>completion_data</c>.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Разбирает и проверяет сохранённую конфигурацию. Ошибка означает, что шаг
    /// некорректен и не должен засчитываться (fail-closed).
    /// </summary>
    /// <param name="json">Значение <c>completion_data</c>.</param>
    /// <param name="configuration">Разобранная конфигурация.</param>
    /// <param name="error">Причина ошибки.</param>
    public static bool TryParse(
        string? json,
        out QuestStepActionConfiguration? configuration,
        out string? error)
    {
        configuration = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Automatic step configuration is empty.";
            return false;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "Automatic step configuration is not valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Automatic step configuration must be a JSON object.";
                return false;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (!PropertyNames.Contains(property.Name))
                {
                    error = $"Unknown automatic step parameter \"{property.Name}\".";
                    return false;
                }
            }

            if (!TryReadString(root, "type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                error = "Automatic step parameter \"type\" is required.";
                return false;
            }

            if (!TryReadInt64(root, "targetId", out var targetId, out error)
                || !TryReadString(root, "achievementCode", out var achievementCode)
                || !TryReadInt64(root, "productId", out var productId, out error)
                || !TryReadInt64(root, "minAmount", out var minAmount, out error)
                || !TryReadInt32(root, "count", out var count, out error)
                || !TryReadBoolean(root, "requireAvatar", out var requireAvatar, out error))
            {
                error ??= "Automatic step configuration contains an invalid value.";
                return false;
            }

            var candidate = new QuestStepActionConfiguration(
                type.Trim().ToLowerInvariant(),
                targetId,
                string.IsNullOrWhiteSpace(achievementCode) ? null : achievementCode.Trim(),
                productId,
                minAmount,
                count,
                requireAvatar);

            if (!QuestStepActionRegistry.TryValidate(candidate, out error))
            {
                return false;
            }

            configuration = candidate;
            return true;
        }
    }

    private static bool TryReadString(JsonElement root, string name, out string? value)
    {
        value = null;

        if (!root.TryGetProperty(name, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static bool TryReadInt64(
        JsonElement root,
        string name,
        out long? value,
        out string? error)
    {
        value = null;
        error = null;

        if (!root.TryGetProperty(name, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var number))
        {
            error = $"Automatic step parameter \"{name}\" must be an integer.";
            return false;
        }

        value = number;
        return true;
    }

    private static bool TryReadInt32(
        JsonElement root,
        string name,
        out int? value,
        out string? error)
    {
        value = null;
        error = null;

        if (!root.TryGetProperty(name, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var number))
        {
            error = $"Automatic step parameter \"{name}\" must be an integer.";
            return false;
        }

        value = number;
        return true;
    }

    private static bool TryReadBoolean(
        JsonElement root,
        string name,
        out bool? value,
        out string? error)
    {
        value = null;
        error = null;

        if (!root.TryGetProperty(name, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = $"Automatic step parameter \"{name}\" must be a boolean.";
            return false;
        }

        value = element.GetBoolean();
        return true;
    }
}

/// <summary>
/// Реестр шаблонов автоматических шагов. Содержит только поддерживаемые сценарии;
/// недоступные шаблоны остаются в реестре, чтобы администратор видел причину.
/// </summary>
public static class QuestStepActionRegistry
{
    private static readonly QuestStepActionTemplate[] Templates =
    [
        new(
            QuestStepActionCodes.CompleteQuest,
            "Пройти квест",
            "Шаг засчитывается, когда сотрудник завершил выбранный квест после присоединения к текущему.",
            QuestStepRetroactivityPolicy.Window,
            [
                new(
                    "targetId",
                    "Квест",
                    "entity",
                    "quests",
                    true,
                    "Квест должен быть завершён после присоединения к текущему квесту.")
            ]),
        new(
            QuestStepActionCodes.CompleteSurvey,
            "Пройти анкету",
            "Шаг засчитывается, когда сотрудник отправил ответ на выбранную анкету. Если повторные ответы запрещены, засчитывается уже отправленный ответ.",
            QuestStepRetroactivityPolicy.Survey,
            [
                new("targetId", "Анкета", "entity", "surveys", true)
            ]),
        new(
            QuestStepActionCodes.SendThanks,
            "Отправить благодарность",
            "Шаг засчитывается, когда сотрудник отправил благодарность коллеге.",
            QuestStepRetroactivityPolicy.Window,
            [
                new(
                    "count",
                    "Количество благодарностей",
                    "number",
                    null,
                    false,
                    "По умолчанию одна благодарность.")
            ]),
        new(
            QuestStepActionCodes.MakePurchase,
            "Совершить покупку",
            "Шаг засчитывается, когда сотрудник оплатил заказ в магазине наград. Отменённые и возвращённые заказы не учитываются.",
            QuestStepRetroactivityPolicy.Window,
            [
                new(
                    "minAmount",
                    "Минимальная сумма заказа",
                    "number",
                    null,
                    false),
                new(
                    "productId",
                    "Товар",
                    "entity",
                    "products",
                    false,
                    "Если выбран товар, он должен быть в заказе.")
            ]),
        new(
            QuestStepActionCodes.AttendEvent,
            "Посетить мероприятие",
            "Шаг засчитывается, когда администратор подтвердил посещение выбранного мероприятия.",
            QuestStepRetroactivityPolicy.State,
            [
                new(
                    "targetId",
                    "Мероприятие",
                    "entity",
                    "events",
                    true,
                    "Посещение подтверждает администратор, регистрация не считается.")
            ]),
        new(
            QuestStepActionCodes.CompleteProfile,
            "Заполнить профиль",
            "Шаг засчитывается, когда у сотрудника установлен аватар. Это ограниченная проверка: другие поля профиля не учитываются.",
            QuestStepRetroactivityPolicy.State,
            [
                new(
                    "requireAvatar",
                    "Требовать аватар",
                    "boolean",
                    null,
                    false,
                    "Единственное поддерживаемое условие — наличие аватара.")
            ]),
        new(
            QuestStepActionCodes.ReceiveThanks,
            "Получить благодарность",
            "Шаг засчитывается, когда сотрудник получил благодарность от коллеги.",
            QuestStepRetroactivityPolicy.Window,
            [
                new(
                    "count",
                    "Количество благодарностей",
                    "number",
                    null,
                    false,
                    "По умолчанию одна благодарность.")
            ]),
        new(
            QuestStepActionCodes.EarnAchievement,
            "Получить достижение",
            "Шаг засчитывается, когда сотрудник получил выбранное достижение. Учитывается уже полученное достижение.",
            QuestStepRetroactivityPolicy.State,
            [
                new(
                    "achievementCode",
                    "Достижение",
                    "entity",
                    "achievements",
                    true)
            ]),
        new(
            QuestStepActionCodes.ReadCompanyNews,
            "Прочитать новость",
            "Шаг должен засчитываться после прочтения выбранной новости.",
            QuestStepRetroactivityPolicy.Window,
            [
                new("targetId", "Новость", "entity", "news", true)
            ],
            IsAvailable: false,
            UnavailableReason:
                "Система пока не фиксирует прочтение новостей: в данных нет отметки о прочтении, "
                + "поэтому шаг нельзя достоверно проверить.")
    ];

    /// <summary>Все известные шаблоны, включая недоступные.</summary>
    public static IReadOnlyList<QuestStepActionTemplate> All => Templates;

    /// <summary>Возвращает шаблон по коду или <c>null</c>.</summary>
    /// <param name="code">Код действия.</param>
    public static QuestStepActionTemplate? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Templates.FirstOrDefault(template =>
                string.Equals(template.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Проверяет, что код относится к известному доступному шаблону.</summary>
    /// <param name="code">Код действия.</param>
    public static bool IsAvailable(string? code) => Find(code) is { IsAvailable: true };

    /// <summary>
    /// Проверяет конфигурацию без обращения к базе: известность шаблона, доступность,
    /// обязательные и лишние параметры, диапазоны значений.
    /// </summary>
    /// <param name="configuration">Конфигурация.</param>
    /// <param name="error">Причина ошибки.</param>
    public static bool TryValidate(
        QuestStepActionConfiguration configuration,
        out string? error)
    {
        error = null;

        var template = Find(configuration.Type);

        if (template is null)
        {
            error = $"Unknown automatic step type \"{configuration.Type}\".";
            return false;
        }

        if (!template.IsAvailable)
        {
            error = template.UnavailableReason ?? $"Automatic step type \"{template.Code}\" is unavailable.";
            return false;
        }

        var hasTarget = template.Parameters.Any(parameter =>
            string.Equals(parameter.Name, "targetId", StringComparison.Ordinal));

        if (hasTarget && configuration.TargetId is not > 0)
        {
            error = $"Automatic step \"{template.Name}\" requires a target object.";
            return false;
        }

        if (!hasTarget && configuration.TargetId is not null)
        {
            error = $"Automatic step \"{template.Name}\" does not accept \"targetId\".";
            return false;
        }

        switch (template.Code)
        {
            case QuestStepActionCodes.CompleteQuest:
            case QuestStepActionCodes.CompleteSurvey:
            case QuestStepActionCodes.AttendEvent:
            case QuestStepActionCodes.ReadCompanyNews:
                if (configuration.AchievementCode is not null
                    || configuration.ProductId is not null
                    || configuration.MinAmount is not null
                    || configuration.Count is not null
                    || configuration.RequireAvatar is not null)
                {
                    error = $"Automatic step \"{template.Name}\" accepts only \"targetId\".";
                    return false;
                }

                break;

            case QuestStepActionCodes.MakePurchase:
                if (configuration.AchievementCode is not null
                    || configuration.Count is not null
                    || configuration.RequireAvatar is not null)
                {
                    error = "Automatic step \"Совершить покупку\" accepts only \"minAmount\" and \"productId\".";
                    return false;
                }

                if (configuration.ProductId is <= 0)
                {
                    error = "ProductId must be a positive identifier.";
                    return false;
                }

                if (configuration.MinAmount is < 0)
                {
                    error = "MinAmount cannot be negative.";
                    return false;
                }

                break;

            case QuestStepActionCodes.SendThanks:
            case QuestStepActionCodes.ReceiveThanks:
                if (configuration.AchievementCode is not null
                    || configuration.ProductId is not null
                    || configuration.MinAmount is not null
                    || configuration.RequireAvatar is not null)
                {
                    error = $"Automatic step \"{template.Name}\" accepts only \"count\".";
                    return false;
                }

                if (configuration.Count is < 1)
                {
                    error = "Count must be greater than zero.";
                    return false;
                }

                break;

            case QuestStepActionCodes.EarnAchievement:
                if (string.IsNullOrWhiteSpace(configuration.AchievementCode))
                {
                    error = "AchievementCode is required.";
                    return false;
                }

                if (configuration.AchievementCode.Trim().Length > 100)
                {
                    error = "AchievementCode must not exceed 100 characters.";
                    return false;
                }

                if (configuration.TargetId is not null
                    || configuration.ProductId is not null
                    || configuration.MinAmount is not null
                    || configuration.Count is not null
                    || configuration.RequireAvatar is not null)
                {
                    error = "Automatic step \"Получить достижение\" accepts only \"achievementCode\".";
                    return false;
                }

                break;

            case QuestStepActionCodes.CompleteProfile:
                if (configuration.RequireAvatar is false)
                {
                    error = "Complete profile requires the avatar check; \"requireAvatar\" cannot be false.";
                    return false;
                }

                if (configuration.TargetId is not null
                    || configuration.AchievementCode is not null
                    || configuration.ProductId is not null
                    || configuration.MinAmount is not null
                    || configuration.Count is not null)
                {
                    error = "Automatic step \"Заполнить профиль\" accepts only \"requireAvatar\".";
                    return false;
                }

                break;
        }

        return true;
    }
}
