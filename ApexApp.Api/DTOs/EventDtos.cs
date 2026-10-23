namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Краткая информация о мероприятии.
    /// </summary>
    /// <param name="Id">Идентификатор мероприятия.</param>
    /// <param name="Title">Название мероприятия.</param>
    /// <param name="Description">Описание мероприятия.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="Location">Место проведения.</param>
    /// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
    /// <param name="RegisteredCount">Количество участников, занимающих место.</param>
    /// <param name="RewardAmount">Размер награды за посещение.</param>
    /// <param name="RegistrationStatus">Статус регистрации текущего пользователя или <c>null</c>.</param>
    public sealed record EventListItemDto(
        long Id,
        string Title,
        string? Description,
        DateTime StartAt,
        DateTime EndAt,
        string? Location,
        int? MaxParticipants,
        int RegisteredCount,
        long RewardAmount,
        string? RegistrationStatus);

    /// <summary>
    /// Участник мероприятия.
    /// </summary>
    /// <param name="Id">Идентификатор регистрации.</param>
    /// <param name="UserId">Идентификатор пользователя.</param>
    /// <param name="FirstName">Имя пользователя.</param>
    /// <param name="LastName">Фамилия пользователя.</param>
    /// <param name="AvatarUrl">Ссылка на аватар пользователя.</param>
    /// <param name="Status">Статус регистрации: <c>registered</c>, <c>attended</c>, <c>cancelled</c> или <c>no_show</c>.</param>
    /// <param name="RegisteredAt">Дата и время регистрации.</param>
    /// <param name="AttendedAt">Дата и время подтверждения посещения.</param>
    /// <param name="RewardClaimed">Признак того, что награда уже выдана.</param>
    public sealed record EventParticipantDto(
        long Id,
        Guid UserId,
        string FirstName,
        string LastName,
        string? AvatarUrl,
        string Status,
        DateTime RegisteredAt,
        DateTime? AttendedAt,
        bool RewardClaimed);

    /// <summary>
    /// Детали мероприятия вместе с регистрацией текущего пользователя.
    /// </summary>
    /// <param name="Id">Идентификатор мероприятия.</param>
    /// <param name="Title">Название мероприятия.</param>
    /// <param name="Description">Описание мероприятия.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="Location">Место проведения.</param>
    /// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
    /// <param name="RegisteredCount">Количество участников, занимающих место.</param>
    /// <param name="RewardAmount">Размер награды за посещение.</param>
    /// <param name="IsActive">Признак активности мероприятия.</param>
    /// <param name="IsOpen">Признак того, что регистрация ещё возможна.</param>
    /// <param name="Registration">Регистрация текущего пользователя или <c>null</c>.</param>
    public sealed record EventDetailsDto(
        long Id,
        string Title,
        string? Description,
        DateTime StartAt,
        DateTime EndAt,
        string? Location,
        int? MaxParticipants,
        int RegisteredCount,
        long RewardAmount,
        bool IsActive,
        bool IsOpen,
        EventParticipantDto? Registration);

    /// <summary>
    /// Мероприятие в административном списке.
    /// </summary>
    /// <param name="Id">Идентификатор мероприятия.</param>
    /// <param name="Title">Название мероприятия.</param>
    /// <param name="Description">Описание мероприятия.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="Location">Место проведения.</param>
    /// <param name="MaxParticipants">Максимальное количество участников или <c>null</c>.</param>
    /// <param name="RegisteredCount">Количество участников, занимающих место.</param>
    /// <param name="RewardAmount">Размер награды за посещение.</param>
    /// <param name="IsActive">Признак активности мероприятия.</param>
    /// <param name="CreatedBy">Идентификатор создателя мероприятия.</param>
    /// <param name="CreatedAt">Дата и время создания.</param>
    /// <param name="UpdatedAt">Дата и время последнего изменения.</param>
    public sealed record AdminEventDto(
        long Id,
        string Title,
        string? Description,
        DateTime StartAt,
        DateTime EndAt,
        string? Location,
        int? MaxParticipants,
        int RegisteredCount,
        long RewardAmount,
        bool IsActive,
        Guid? CreatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
