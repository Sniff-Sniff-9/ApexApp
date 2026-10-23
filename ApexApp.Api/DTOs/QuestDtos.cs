namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Краткая информация о доступном квесте.
    /// </summary>
    /// <param name="Id">Идентификатор квеста.</param>
    /// <param name="Title">Название квеста.</param>
    /// <param name="Description">Описание квеста.</param>
    /// <param name="RewardAmount">Размер награды.</param>
    /// <param name="StartAt">Дата и время начала квеста.</param>
    /// <param name="EndAt">Дата и время окончания квеста.</param>
    /// <param name="StepsCount">Общее количество шагов.</param>
    /// <param name="RequiredStepsCount">Количество обязательных шагов.</param>
    /// <param name="ParticipationStatus">Статус участия текущего пользователя или <c>null</c>, если участия нет.</param>
    public sealed record QuestListItemDto(
        long Id,
        string Title,
        string? Description,
        long RewardAmount,
        DateTime? StartAt,
        DateTime? EndAt,
        int StepsCount,
        int RequiredStepsCount,
        string? ParticipationStatus);

    /// <summary>
    /// Шаг квеста с состоянием выполнения текущим пользователем.
    /// </summary>
    /// <param name="Id">Идентификатор шага.</param>
    /// <param name="Title">Название шага.</param>
    /// <param name="Description">Описание шага.</param>
    /// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
    /// <param name="SortOrder">Порядок отображения шага.</param>
    /// <param name="IsRequired">Признак обязательного шага.</param>
    /// <param name="IsCompleted">Признак подтверждённого выполнения шага.</param>
    /// <param name="CompletedAt">Дата и время завершения шага.</param>
    /// <param name="ProofStatus">Статус последнего доказательства.</param>
    /// <param name="ProofStoragePath">Путь к последнему доказательству.</param>
    /// <param name="ProofComment">Комментарий к последнему доказательству.</param>
    public sealed record QuestStepDto(
        long Id,
        string Title,
        string? Description,
        string CompletionType,
        int SortOrder,
        bool IsRequired,
        bool IsCompleted,
        DateTime? CompletedAt,
        string? ProofStatus,
        string? ProofStoragePath,
        string? ProofComment);

    /// <summary>
    /// Участие пользователя в квесте.
    /// </summary>
    /// <param name="Id">Идентификатор участия.</param>
    /// <param name="Status">Статус участия: <c>in_progress</c>, <c>completed</c> или <c>cancelled</c>.</param>
    /// <param name="StartedAt">Дата и время начала участия.</param>
    /// <param name="CompletedAt">Дата и время завершения квеста.</param>
    /// <param name="RewardClaimed">Признак того, что награда уже обработана.</param>
    public sealed record QuestParticipationDto(
        long Id,
        string Status,
        DateTime StartedAt,
        DateTime? CompletedAt,
        bool RewardClaimed);

    /// <summary>
    /// Детали квеста вместе с участием текущего пользователя.
    /// </summary>
    /// <param name="Id">Идентификатор квеста.</param>
    /// <param name="Title">Название квеста.</param>
    /// <param name="Description">Описание квеста.</param>
    /// <param name="RewardAmount">Размер награды.</param>
    /// <param name="IsActive">Признак активности квеста.</param>
    /// <param name="StartAt">Дата и время начала квеста.</param>
    /// <param name="EndAt">Дата и время окончания квеста.</param>
    /// <param name="Participation">Участие текущего пользователя или <c>null</c>.</param>
    /// <param name="Steps">Шаги квеста в порядке отображения.</param>
    public sealed record QuestDetailsDto(
        long Id,
        string Title,
        string? Description,
        long RewardAmount,
        bool IsActive,
        DateTime? StartAt,
        DateTime? EndAt,
        QuestParticipationDto? Participation,
        IReadOnlyList<QuestStepDto> Steps);

    /// <summary>
    /// Квест в административном списке.
    /// </summary>
    /// <param name="Id">Идентификатор квеста.</param>
    /// <param name="Title">Название квеста.</param>
    /// <param name="Description">Описание квеста.</param>
    /// <param name="RewardAmount">Размер награды.</param>
    /// <param name="IsActive">Признак активности квеста.</param>
    /// <param name="StartAt">Дата и время начала квеста.</param>
    /// <param name="EndAt">Дата и время окончания квеста.</param>
    /// <param name="StepsCount">Количество шагов.</param>
    /// <param name="ParticipantsCount">Количество участий.</param>
    /// <param name="PendingProofsCount">Количество доказательств, ожидающих проверки.</param>
    /// <param name="CreatedBy">Идентификатор создателя.</param>
    /// <param name="CreatedAt">Дата и время создания.</param>
    /// <param name="UpdatedAt">Дата и время последнего изменения.</param>
    public sealed record AdminQuestDto(
        long Id,
        string Title,
        string? Description,
        long RewardAmount,
        bool IsActive,
        DateTime? StartAt,
        DateTime? EndAt,
        int StepsCount,
        int ParticipantsCount,
        int PendingProofsCount,
        Guid? CreatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    /// <summary>
    /// Шаг квеста в административном представлении.
    /// </summary>
    /// <param name="Id">Идентификатор шага.</param>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="Title">Название шага.</param>
    /// <param name="Description">Описание шага.</param>
    /// <param name="CompletionType">Тип шага: <c>manual</c>, <c>proof</c> или <c>automatic</c>.</param>
    /// <param name="SortOrder">Порядок отображения шага.</param>
    /// <param name="IsRequired">Признак обязательного шага.</param>
    /// <param name="CompletionData">Дополнительные данные шага в формате JSON.</param>
    /// <param name="CreatedAt">Дата и время создания шага.</param>
    public sealed record AdminQuestStepDto(
        long Id,
        long QuestId,
        string Title,
        string? Description,
        string CompletionType,
        int SortOrder,
        bool IsRequired,
        string? CompletionData,
        DateTime CreatedAt);
}
