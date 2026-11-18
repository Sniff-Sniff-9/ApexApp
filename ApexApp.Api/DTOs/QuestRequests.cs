namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Запрос на завершение шага квеста.
    /// </summary>
    /// <param name="StoragePath">
    /// Путь к загруженному доказательству. Обязателен для шагов типа <c>proof</c>.
    /// </param>
    /// <param name="Comment">Необязательный комментарий пользователя к доказательству.</param>
    public sealed record CompleteQuestStepRequest(
        string? StoragePath,
        string? Comment);

    /// <summary>
    /// Запрос на создание квеста.
    /// </summary>
    /// <param name="Title">Название квеста (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание квеста.</param>
    /// <param name="RewardAmount">Размер награды (не может быть отрицательным).</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="IsActive">Признак активности квеста.</param>
    /// <param name="Steps">Шаги нового квеста; требуется хотя бы один.</param>
    public sealed record CreateQuestRequest(
        string? Title,
        string? Description,
        long? RewardAmount,
        DateTime? StartAt,
        DateTime? EndAt,
        bool IsActive,
        IReadOnlyList<CreateQuestStepRequest>? Steps = null);

    /// <summary>
    /// Запрос на обновление квеста.
    /// </summary>
    /// <param name="Title">Название квеста (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание квеста.</param>
    /// <param name="RewardAmount">Размер награды (не может быть отрицательным).</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="IsActive">Признак активности квеста.</param>
    public sealed record UpdateQuestRequest(
        string? Title,
        string? Description,
        long? RewardAmount,
        DateTime? StartAt,
        DateTime? EndAt,
        bool IsActive);

    /// <summary>
    /// Запрос на создание шага квеста.
    /// </summary>
    /// <param name="Title">Название шага (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание шага.</param>
    /// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>; по умолчанию <c>manual</c>.</param>
    /// <param name="SortOrder">Порядок отображения; по умолчанию 0.</param>
    /// <param name="IsRequired">Признак обязательного шага; по умолчанию <c>true</c>.</param>
    /// <param name="CompletionData">Дополнительные данные шага в формате JSON.</param>
    /// <param name="Action">Типизированные параметры автоматического шага.</param>
    public sealed record CreateQuestStepRequest(
        string? Title,
        string? Description,
        string? CompletionType,
        int? SortOrder,
        bool? IsRequired,
        string? CompletionData,
        QuestStepActionRequest? Action = null);

    /// <summary>
    /// Запрос на обновление шага квеста.
    /// </summary>
    /// <param name="Title">Название шага (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание шага.</param>
    /// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>; по умолчанию <c>manual</c>.</param>
    /// <param name="SortOrder">Порядок отображения; по умолчанию 0.</param>
    /// <param name="IsRequired">Признак обязательного шага; по умолчанию <c>true</c>.</param>
    /// <param name="CompletionData">Дополнительные данные шага в формате JSON.</param>
    /// <param name="Action">Типизированные параметры автоматического шага.</param>
    public sealed record UpdateQuestStepRequest(
        string? Title,
        string? Description,
        string? CompletionType,
        int? SortOrder,
        bool? IsRequired,
        string? CompletionData,
        QuestStepActionRequest? Action = null);

    /// <summary>
    /// Параметры автоматического действия, выбранные администратором в форме.
    /// </summary>
    /// <param name="Type">Код действия из каталога шаблонов.</param>
    /// <param name="TargetId">Идентификатор выбранного объекта.</param>
    /// <param name="AchievementCode">Код достижения.</param>
    /// <param name="ProductId">Товар для покупки.</param>
    /// <param name="MinAmount">Минимальная сумма заказа.</param>
    /// <param name="Count">Требуемое количество действий.</param>
    /// <param name="RequireAvatar">Требование аватара.</param>
    public sealed record QuestStepActionRequest(
        string? Type,
        long? TargetId = null,
        string? AchievementCode = null,
        long? ProductId = null,
        long? MinAmount = null,
        int? Count = null,
        bool? RequireAvatar = null);

    /// <summary>
    /// Запрос на проверку доказательства.
    /// </summary>
    /// <param name="Status">Новый статус: <c>approved</c> или <c>rejected</c>.</param>
    public sealed record ReviewQuestProofRequest(string? Status);
}
