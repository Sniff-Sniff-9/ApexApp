namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Результат регистрации на мероприятие.
    /// </summary>
    /// <param name="EventId">Идентификатор мероприятия.</param>
    /// <param name="ParticipantId">Идентификатор регистрации.</param>
    /// <param name="Status">Статус регистрации.</param>
    /// <param name="RegisteredAt">Дата и время регистрации.</param>
    /// <param name="AlreadyRegistered">Признак того, что действующая регистрация уже существовала.</param>
    public sealed record EventRegistrationResultDto(
        long EventId,
        long ParticipantId,
        string Status,
        DateTime RegisteredAt,
        bool AlreadyRegistered);

    /// <summary>
    /// Результат отмены регистрации на мероприятие.
    /// </summary>
    /// <param name="EventId">Идентификатор мероприятия.</param>
    /// <param name="ParticipantId">Идентификатор регистрации.</param>
    /// <param name="Status">Статус регистрации после отмены.</param>
    /// <param name="AlreadyCancelled">Признак того, что регистрация уже была отменена.</param>
    public sealed record EventCancellationResultDto(
        long EventId,
        long ParticipantId,
        string Status,
        bool AlreadyCancelled);

    /// <summary>
    /// Результат начисления награды за посещение мероприятия.
    /// </summary>
    /// <param name="EventId">Идентификатор мероприятия.</param>
    /// <param name="Amount">Начисленная сумма.</param>
    /// <param name="BalanceAfter">Остаток личного баланса после начисления.</param>
    /// <param name="AlreadyGranted">Признак того, что награда уже была выдана ранее.</param>
    public sealed record EventRewardResultDto(
        long EventId,
        long Amount,
        long? BalanceAfter,
        bool AlreadyGranted);

    /// <summary>
    /// Результат изменения посещаемости мероприятия.
    /// </summary>
    /// <param name="EventId">Идентификатор мероприятия.</param>
    /// <param name="ParticipantId">Идентификатор регистрации.</param>
    /// <param name="Status">Текущий статус участника.</param>
    /// <param name="AttendedAt">Дата и время подтверждения посещения.</param>
    /// <param name="AlreadyInStatus">Признак того, что участник уже находился в этом статусе.</param>
    /// <param name="NewAchievement">
    /// Достижение за первое подтверждённое участие или <c>null</c>, если оно не выдавалось.
    /// </param>
    public sealed record EventAttendanceResultDto(
        long EventId,
        long ParticipantId,
        string Status,
        DateTime? AttendedAt,
        bool AlreadyInStatus,
        AchievementDto? NewAchievement = null);
}
