using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Команда завершения шага квеста.
/// </summary>
/// <param name="StoragePath">Путь к загруженному доказательству; обязателен для шагов типа <c>proof</c>.</param>
/// <param name="Comment">Необязательный комментарий пользователя к доказательству.</param>
public sealed record CompleteQuestStepCommand(string? StoragePath, string? Comment);

/// <summary>
/// Краткая информация о доступном квесте для списка.
/// </summary>
/// <param name="Quest">Квест.</param>
/// <param name="StepsCount">Общее количество шагов.</param>
/// <param name="RequiredStepsCount">Количество обязательных шагов.</param>
/// <param name="ParticipationStatus">Статус участия текущего пользователя или <c>null</c>, если участия нет.</param>
public sealed record QuestSummary(
    Quest Quest,
    int StepsCount,
    int RequiredStepsCount,
    string? ParticipationStatus);

/// <summary>
/// Участие пользователя в квесте с прогрессом выполнения шагов.
/// </summary>
/// <param name="Participation">Участие вместе с квестом и его шагами.</param>
/// <param name="TotalSteps">Общее количество шагов квеста.</param>
/// <param name="CompletedSteps">Количество шагов, выполнение которых подтверждено.</param>
public sealed record MyQuestSummary(
    QuestParticipation Participation,
    int TotalSteps,
    int CompletedSteps);

/// <summary>
/// Состояние шага квеста для текущего пользователя.
/// </summary>
/// <param name="Step">Шаг квеста.</param>
/// <param name="Completion">Завершение шага текущим пользователем или <c>null</c>.</param>
/// <param name="LatestProof">Последнее доказательство по завершению шага или <c>null</c>.</param>
public sealed record QuestStepState(
    QuestStep Step,
    QuestStepCompletion? Completion,
    QuestProof? LatestProof);

/// <summary>
/// Детали квеста вместе с участием текущего пользователя.
/// </summary>
/// <param name="Quest">Квест.</param>
/// <param name="Participation">Участие текущего пользователя или <c>null</c>.</param>
/// <param name="Steps">Шаги квеста, упорядоченные по <c>sort_order</c>, затем по идентификатору.</param>
public sealed record QuestDetailsResult(
    Quest Quest,
    QuestParticipation? Participation,
    IReadOnlyList<QuestStepState> Steps);

/// <summary>
/// Результат присоединения к квесту.
/// </summary>
/// <param name="Participation">Существующее или созданное участие.</param>
/// <param name="AlreadyParticipating">Признак того, что участие уже существовало.</param>
public sealed record QuestJoinResult(
    QuestParticipation Participation,
    bool AlreadyParticipating);

/// <summary>
/// Результат завершения шага квеста.
/// </summary>
/// <param name="Completion">Завершение шага.</param>
/// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="Completed">Признак подтверждённого выполнения шага.</param>
/// <param name="ProofStatus">Статус последнего доказательства или <c>null</c> для шагов без доказательства.</param>
/// <param name="AlreadyCompleted">Признак того, что шаг уже был выполнен ранее.</param>
public sealed record QuestStepResult(
    QuestStepCompletion Completion,
    string CompletionType,
    bool Completed,
    string? ProofStatus,
    bool AlreadyCompleted);

/// <summary>
/// Результат завершения квеста и начисления награды.
/// </summary>
/// <param name="Participation">Завершённое участие.</param>
/// <param name="RewardAmount">Размер награды, настроенный в квесте.</param>
/// <param name="BalanceAfter">Остаток личного баланса после начисления или <c>null</c>, если баланс отсутствует.</param>
/// <param name="RewardGranted">Признак того, что награда была начислена этим запросом.</param>
/// <param name="AlreadyCompleted">Признак того, что квест уже был завершён ранее.</param>
public sealed record QuestCompletionResult(
    QuestParticipation Participation,
    long RewardAmount,
    long? BalanceAfter,
    bool RewardGranted,
    bool AlreadyCompleted);

/// <summary>
/// Строковые значения перечисления <see cref="QuestStepCompletionType"/>, совпадающие
/// со значениями типа БД <c>quest_step_completion_type</c>.
/// </summary>
public static class QuestStepCompletionTypeNames
{
    /// <summary>Ручное подтверждение выполнения шага пользователем.</summary>
    public const string Manual = "manual";

    /// <summary>Выполнение шага подтверждается загруженным доказательством.</summary>
    public const string Proof = "proof";

    /// <summary>Автоматическое выполнение шага (не поддерживается в текущей фазе).</summary>
    public const string Automatic = "automatic";

    /// <summary>
    /// Преобразует тип завершения шага в значение, совпадающее с типом БД.
    /// </summary>
    /// <param name="completionType">Тип завершения шага.</param>
    /// <returns>Строковое значение типа БД.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Передан неизвестный тип завершения шага.</exception>
    public static string ToDbValue(QuestStepCompletionType completionType) => completionType switch
    {
        QuestStepCompletionType.Manual => Manual,
        QuestStepCompletionType.Proof => Proof,
        QuestStepCompletionType.Automatic => Automatic,
        _ => throw new ArgumentOutOfRangeException(
            nameof(completionType),
            completionType,
            "Unknown quest step completion type.")
    };

    /// <summary>
    /// Разбирает строковое значение типа завершения шага без учёта регистра.
    /// </summary>
    /// <param name="value">Значение из запроса.</param>
    /// <param name="completionType">Разобранный тип завершения шага.</param>
    /// <returns><c>true</c>, если значение соответствует поддерживаемому типу.</returns>
    public static bool TryParse(string? value, out QuestStepCompletionType completionType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case Manual:
                completionType = QuestStepCompletionType.Manual;
                return true;
            case Proof:
                completionType = QuestStepCompletionType.Proof;
                return true;
            case Automatic:
                completionType = QuestStepCompletionType.Automatic;
                return true;
            default:
                completionType = QuestStepCompletionType.Manual;
                return false;
        }
    }
}

/// <summary>
/// Квест в административном списке.
/// </summary>
/// <param name="Quest">Квест.</param>
/// <param name="StepsCount">Количество шагов.</param>
/// <param name="ParticipantsCount">Количество участий.</param>
/// <param name="PendingProofsCount">Количество доказательств, ожидающих проверки.</param>
public sealed record AdminQuestSummary(
    Quest Quest,
    int StepsCount,
    int ParticipantsCount,
    int PendingProofsCount);

/// <summary>
/// Доказательство вместе с контекстом квеста, шага и участника.
/// </summary>
/// <param name="Proof">Доказательство.</param>
/// <param name="QuestId">Идентификатор квеста.</param>
/// <param name="QuestTitle">Название квеста.</param>
/// <param name="StepId">Идентификатор шага.</param>
/// <param name="StepTitle">Название шага.</param>
/// <param name="UserId">Идентификатор участника, загрузившего доказательство.</param>
public sealed record QuestProofView(
    QuestProof Proof,
    long QuestId,
    string QuestTitle,
    long StepId,
    string StepTitle,
    Guid UserId);

/// <summary>
/// Результат проверки доказательства.
/// </summary>
/// <param name="Proof">Проверенное доказательство вместе с контекстом.</param>
/// <param name="AlreadyReviewed">Признак того, что доказательство уже имело запрошенный статус.</param>
public sealed record QuestProofReviewResult(QuestProofView Proof, bool AlreadyReviewed);

/// <summary>
/// Данные для создания или обновления квеста.
/// </summary>
/// <param name="Title">Название квеста (обязательно, до 200 символов).</param>
/// <param name="Description">Описание квеста.</param>
/// <param name="RewardAmount">Размер награды (не может быть отрицательным).</param>
/// <param name="StartAt">Дата и время начала.</param>
/// <param name="EndAt">Дата и время окончания (позже начала).</param>
/// <param name="IsActive">Признак активности квеста.</param>
public sealed record QuestCommand(
    string? Title,
    string? Description,
    long? RewardAmount,
    DateTime? StartAt,
    DateTime? EndAt,
    bool IsActive);

/// <summary>
/// Данные для создания или обновления шага квеста.
/// </summary>
/// <param name="Title">Название шага (обязательно, до 200 символов).</param>
/// <param name="Description">Описание шага.</param>
/// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
/// <param name="SortOrder">Порядок отображения (не может быть отрицательным).</param>
/// <param name="IsRequired">Признак обязательного шага.</param>
/// <param name="CompletionData">Дополнительные данные шага в формате JSON.</param>
public sealed record QuestStepCommand(
    string? Title,
    string? Description,
    string? CompletionType,
    int SortOrder,
    bool IsRequired,
    string? CompletionData);

/// <summary>
/// Команда проверки доказательства.
/// </summary>
/// <param name="Status">Новый статус: <c>approved</c> или <c>rejected</c>.</param>
public sealed record ReviewQuestProofCommand(string? Status);
