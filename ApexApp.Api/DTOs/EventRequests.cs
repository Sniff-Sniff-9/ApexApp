namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Запрос на создание мероприятия.
    /// </summary>
    /// <param name="Title">Название мероприятия (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание мероприятия.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="Location">Место проведения.</param>
    /// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
    /// <param name="RewardAmount">Размер награды за посещение (не может быть отрицательным).</param>
    /// <param name="IsActive">Признак активности мероприятия.</param>
    public sealed record CreateEventRequest(
        string? Title,
        string? Description,
        DateTime? StartAt,
        DateTime? EndAt,
        string? Location,
        int? MaxParticipants,
        long? RewardAmount,
        bool IsActive);

    /// <summary>
    /// Запрос на обновление мероприятия.
    /// </summary>
    /// <param name="Title">Название мероприятия (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание мероприятия.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="Location">Место проведения.</param>
    /// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
    /// <param name="RewardAmount">Размер награды за посещение (не может быть отрицательным).</param>
    /// <param name="IsActive">Признак активности мероприятия.</param>
    public sealed record UpdateEventRequest(
        string? Title,
        string? Description,
        DateTime? StartAt,
        DateTime? EndAt,
        string? Location,
        int? MaxParticipants,
        long? RewardAmount,
        bool IsActive);

    /// <summary>
    /// Запрос на изменение посещаемости участника мероприятия.
    /// </summary>
    /// <param name="Status">Новый статус: <c>attended</c> или <c>no_show</c>.</param>
    public sealed record SetEventAttendanceRequest(string? Status);
}
