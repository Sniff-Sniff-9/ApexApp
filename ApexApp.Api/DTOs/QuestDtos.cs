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
    /// Участник квеста в административном представлении.
    /// </summary>
    /// <param name="ParticipationId">Идентификатор участия.</param>
    /// <param name="UserId">Идентификатор пользователя.</param>
    /// <param name="FirstName">Имя пользователя.</param>
    /// <param name="LastName">Фамилия пользователя.</param>
    /// <param name="AvatarUrl">Ссылка на аватар.</param>
    /// <param name="Status">Статус участия.</param>
    /// <param name="StartedAt">Дата и время начала участия.</param>
    /// <param name="CompletedAt">Дата и время завершения.</param>
    /// <param name="RewardClaimed">Признак обработанной награды.</param>
    /// <param name="TotalSteps">Общее количество шагов.</param>
    /// <param name="CompletedSteps">Количество подтверждённых шагов.</param>
    /// <param name="PendingProofs">Количество доказательств на проверке.</param>
    public sealed record AdminQuestParticipantDto(
        long ParticipationId,
        Guid UserId,
        string FirstName,
        string LastName,
        string? AvatarUrl,
        string Status,
        DateTime StartedAt,
        DateTime? CompletedAt,
        bool RewardClaimed,
        int TotalSteps,
        int CompletedSteps,
        int PendingProofs);

    /// <summary>
    /// Сводные показатели квеста.
    /// </summary>
    /// <param name="QuestId">Идентификатор квеста.</param>
    /// <param name="TotalParticipants">Общее количество участий.</param>
    /// <param name="InProgress">Количество активных участий.</param>
    /// <param name="Completed">Количество завершённых участий.</param>
    /// <param name="Cancelled">Количество отменённых участий.</param>
    /// <param name="RewardClaimed">Количество участий с обработанной наградой.</param>
    /// <param name="PendingProofs">Количество доказательств на проверке.</param>
    public sealed record QuestStatsDto(
        long QuestId,
        int TotalParticipants,
        int InProgress,
        int Completed,
        int Cancelled,
        int RewardClaimed,
        int PendingProofs);

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
        DateTime CreatedAt,
        QuestStepActionDto? Action = null,
        string? ConfigurationError = null);

    /// <summary>
    /// Типизированные параметры автоматического шага для административной формы.
    /// </summary>
    /// <param name="Type">Код действия.</param>
    /// <param name="Name">Понятное название действия.</param>
    /// <param name="Description">Понятное описание условия.</param>
    /// <param name="TargetId">Идентификатор выбранного объекта.</param>
    /// <param name="AchievementCode">Код достижения.</param>
    /// <param name="ProductId">Товар для покупки.</param>
    /// <param name="MinAmount">Минимальная сумма заказа.</param>
    /// <param name="Count">Требуемое количество действий.</param>
    /// <param name="RequireAvatar">Требование аватара.</param>
    public sealed record QuestStepActionDto(
        string Type,
        string Name,
        string Description,
        long? TargetId,
        string? AchievementCode,
        long? ProductId,
        long? MinAmount,
        int? Count,
        bool? RequireAvatar);

    /// <summary>
    /// Описание шаблона автоматического шага для административной формы.
    /// </summary>
    /// <param name="Code">Код действия.</param>
    /// <param name="Name">Понятное название.</param>
    /// <param name="Description">Понятное описание условия.</param>
    /// <param name="IsAvailable">Признак доступности шаблона.</param>
    /// <param name="UnavailableReason">Причина недоступности.</param>
    /// <param name="Parameters">Параметры шаблона.</param>
    public sealed record QuestStepActionTemplateDto(
        string Code,
        string Name,
        string Description,
        bool IsAvailable,
        string? UnavailableReason,
        IReadOnlyList<QuestStepActionParameterDto> Parameters);

    /// <summary>
    /// Описание параметра шаблона автоматического шага.
    /// </summary>
    /// <param name="Name">Имя параметра.</param>
    /// <param name="Label">Подпись для администратора.</param>
    /// <param name="Kind">Тип элемента управления: <c>entity</c>, <c>number</c>, <c>boolean</c>.</param>
    /// <param name="OptionsSource">Источник вариантов для <c>entity</c>.</param>
    /// <param name="Required">Признак обязательного параметра.</param>
    /// <param name="HelpText">Пояснение.</param>
    public sealed record QuestStepActionParameterDto(
        string Name,
        string Label,
        string Kind,
        string? OptionsSource,
        bool Required,
        string? HelpText);

    /// <summary>
    /// Вариант выбора объекта для параметра автоматического шага.
    /// </summary>
    /// <param name="Id">Идентификатор объекта или код достижения.</param>
    /// <param name="Title">Название.</param>
    /// <param name="Subtitle">Дополнительная подпись.</param>
    /// <param name="IsActive">Признак доступности объекта.</param>
    public sealed record QuestStepActionOptionDto(
        string Id,
        string Title,
        string? Subtitle,
        bool IsActive);

    /// <summary>
    /// Шаблон автоматического шага и варианты выбора его объектов.
    /// </summary>
    /// <param name="Template">Описание шаблона.</param>
    /// <param name="Options">Варианты выбора.</param>
    public sealed record QuestStepActionOptionsDto(
        QuestStepActionTemplateDto Template,
        IReadOnlyList<QuestStepActionOptionDto> Options);
}
