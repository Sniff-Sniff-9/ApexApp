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
    public sealed record CreateQuestRequest(
        string? Title,
        string? Description,
        long? RewardAmount,
        DateTime? StartAt,
        DateTime? EndAt,
        bool IsActive);

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
    public sealed record CreateQuestStepRequest(
        string? Title,
        string? Description,
        string? CompletionType,
        int? SortOrder,
        bool? IsRequired,
        string? CompletionData);

    /// <summary>
    /// Запрос на обновление шага квеста.
    /// </summary>
    /// <param name="Title">Название шага (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание шага.</param>
    /// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>; по умолчанию <c>manual</c>.</param>
    /// <param name="SortOrder">Порядок отображения; по умолчанию 0.</param>
    /// <param name="IsRequired">Признак обязательного шага; по умолчанию <c>true</c>.</param>
    /// <param name="CompletionData">Дополнительные данные шага в формате JSON.</param>
    public sealed record UpdateQuestStepRequest(
        string? Title,
        string? Description,
        string? CompletionType,
        int? SortOrder,
        bool? IsRequired,
        string? CompletionData);

    /// <summary>
    /// Запрос на проверку доказательства.
    /// </summary>
    /// <param name="Status">Новый статус: <c>approved</c> или <c>rejected</c>.</param>
    public sealed record ReviewQuestProofRequest(string? Status);
}
