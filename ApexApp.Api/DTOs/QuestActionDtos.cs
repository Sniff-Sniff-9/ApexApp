namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Участие текущего пользователя в квесте с прогрессом выполнения.
    /// </summary>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="Title">Название квеста.</param>
    /// <param name="RewardAmount">Размер награды.</param>
    /// <param name="Status">Статус участия.</param>
    /// <param name="StartedAt">Дата и время начала участия.</param>
    /// <param name="CompletedAt">Дата и время завершения квеста.</param>
    /// <param name="RewardClaimed">Признак того, что награда уже обработана.</param>
    /// <param name="QuestStartAt">Дата и время начала квеста.</param>
    /// <param name="QuestEndAt">Дата и время окончания квеста.</param>
    /// <param name="TotalSteps">Общее количество шагов квеста.</param>
    /// <param name="CompletedSteps">Количество шагов, выполнение которых подтверждено.</param>
    public sealed record MyQuestDto(
        long QuestId,
        string Title,
        long RewardAmount,
        string Status,
        DateTime StartedAt,
        DateTime? CompletedAt,
        bool RewardClaimed,
        DateTime? QuestStartAt,
        DateTime? QuestEndAt,
        int TotalSteps,
        int CompletedSteps);

    /// <summary>
    /// Результат присоединения к квесту.
    /// </summary>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="ParticipationId">Идентификатор участия.</param>
    /// <param name="Status">Статус участия.</param>
    /// <param name="StartedAt">Дата и время начала участия.</param>
    /// <param name="AlreadyParticipating">Признак того, что участие уже существовало.</param>
    public sealed record JoinQuestResultDto(
        long QuestId,
        long ParticipationId,
        string Status,
        DateTime StartedAt,
        bool AlreadyParticipating);

    /// <summary>
    /// Результат завершения шага квеста.
    /// </summary>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="StepId">Идентификатор шага.</param>
    /// <param name="CompletionType">Тип шага.</param>
    /// <param name="Completed">Признак подтверждённого выполнения шага.</param>
    /// <param name="ProofStatus">Статус последнего доказательства.</param>
    /// <param name="CompletedAt">Дата и время завершения шага.</param>
    /// <param name="AlreadyCompleted">Признак того, что шаг уже был выполнен ранее.</param>
    public sealed record QuestStepResultDto(
        long QuestId,
        long StepId,
        string CompletionType,
        bool Completed,
        string? ProofStatus,
        DateTime? CompletedAt,
        bool AlreadyCompleted);

    /// <summary>
    /// Результат завершения квеста и начисления награды.
    /// </summary>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="ParticipationId">Идентификатор участия.</param>
    /// <param name="Status">Статус участия.</param>
    /// <param name="CompletedAt">Дата и время завершения квеста.</param>
    /// <param name="RewardAmount">Размер награды, настроенный в квесте.</param>
    /// <param name="BalanceAfter">Остаток личного баланса после начисления.</param>
    /// <param name="RewardGranted">Признак того, что награда начислена этим запросом.</param>
    /// <param name="AlreadyCompleted">Признак того, что квест уже был завершён ранее.</param>
    public sealed record CompleteQuestResultDto(
        long QuestId,
        long ParticipationId,
        string Status,
        DateTime? CompletedAt,
        long RewardAmount,
        long? BalanceAfter,
        bool RewardGranted,
        bool AlreadyCompleted);

    /// <summary>
    /// Доказательство выполнения шага квеста для модерации.
    /// </summary>
    /// <param name="Id">Идентификатор доказательства.</param>
    /// <param name="CompletionId">Идентификатор завершения шага.</param>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="QuestTitle">Название квеста.</param>
    /// <param name="StepId">Идентификатор шага.</param>
    /// <param name="StepTitle">Название шага.</param>
    /// <param name="UserId">Идентификатор участника.</param>
    /// <param name="StoragePath">Путь к загруженному доказательству.</param>
    /// <param name="Comment">Комментарий участника.</param>
    /// <param name="Status">Статус: <c>pending</c>, <c>approved</c> или <c>rejected</c>.</param>
    /// <param name="ReviewedBy">Идентификатор проверяющего.</param>
    /// <param name="ReviewedAt">Дата и время проверки.</param>
    /// <param name="CreatedAt">Дата и время загрузки доказательства.</param>
    public sealed record QuestProofDto(
        long Id,
        long CompletionId,
        long QuestId,
        string QuestTitle,
        long StepId,
        string StepTitle,
        Guid UserId,
        string StoragePath,
        string? Comment,
        string Status,
        Guid? ReviewedBy,
        DateTime? ReviewedAt,
        DateTime CreatedAt);

    /// <summary>
    /// Результат проверки доказательства.
    /// </summary>
    /// <param name="Proof">Проверенное доказательство.</param>
    /// <param name="AlreadyReviewed">Признак того, что доказательство уже имело запрошенный статус.</param>
    public sealed record ReviewQuestProofResultDto(QuestProofDto Proof, bool AlreadyReviewed);
}
