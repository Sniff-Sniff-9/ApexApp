using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Мероприятие вместе с количеством участников и регистрацией текущего пользователя.
/// </summary>
/// <param name="Event">Мероприятие.</param>
/// <param name="RegisteredCount">Количество участников со статусом <c>registered</c> или <c>attended</c>.</param>
/// <param name="Registration">Регистрация текущего пользователя или <c>null</c>.</param>
public sealed record EventSummary(
    Event Event,
    int RegisteredCount,
    EventParticipant? Registration);

/// <summary>
/// Детали мероприятия вместе с регистрацией текущего пользователя.
/// </summary>
/// <param name="Event">Мероприятие.</param>
/// <param name="RegisteredCount">Количество участников со статусом <c>registered</c> или <c>attended</c>.</param>
/// <param name="Registration">Регистрация текущего пользователя или <c>null</c>.</param>
/// <param name="IsOpen">Признак того, что регистрация на мероприятие ещё возможна.</param>
public sealed record EventDetailsResult(
    Event Event,
    int RegisteredCount,
    EventParticipant? Registration,
    bool IsOpen);

/// <summary>
/// Результат регистрации на мероприятие.
/// </summary>
/// <param name="Registration">Существующая или созданная регистрация.</param>
/// <param name="AlreadyRegistered">Признак того, что действующая регистрация уже существовала.</param>
public sealed record EventRegistrationResult(
    EventParticipant Registration,
    bool AlreadyRegistered);

/// <summary>
/// Результат отмены регистрации на мероприятие.
/// </summary>
/// <param name="Registration">Регистрация после отмены.</param>
/// <param name="AlreadyCancelled">Признак того, что регистрация уже была отменена.</param>
public sealed record EventCancellationResult(
    EventParticipant Registration,
    bool AlreadyCancelled);

/// <summary>
/// Результат изменения посещаемости мероприятия.
/// </summary>
/// <param name="Participant">Участник мероприятия.</param>
/// <param name="AlreadyInStatus">Признак того, что участник уже находился в запрошенном статусе.</param>
/// <param name="NewAchievement">
/// Достижение за первое подтверждённое участие или <c>null</c>, если оно не выдавалось.
/// </param>
public sealed record EventAttendanceResult(
    EventParticipant Participant,
    bool AlreadyInStatus,
    Achievement? NewAchievement = null);

/// <summary>
/// Данные для создания мероприятия.
/// </summary>
/// <param name="Title">Название мероприятия.</param>
/// <param name="Description">Описание мероприятия.</param>
/// <param name="StartAt">Дата и время начала.</param>
/// <param name="EndAt">Дата и время окончания.</param>
/// <param name="Location">Место проведения.</param>
/// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
/// <param name="RewardAmount">Размер награды за посещение.</param>
/// <param name="IsActive">Признак активности мероприятия.</param>
public sealed record CreateEventCommand(
    string? Title,
    string? Description,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    int? MaxParticipants,
    long? RewardAmount,
    bool IsActive);

/// <summary>
/// Данные для обновления мероприятия.
/// </summary>
/// <param name="Title">Название мероприятия.</param>
/// <param name="Description">Описание мероприятия.</param>
/// <param name="StartAt">Дата и время начала.</param>
/// <param name="EndAt">Дата и время окончания.</param>
/// <param name="Location">Место проведения.</param>
/// <param name="MaxParticipants">Максимальное количество участников или <c>null</c> без ограничения.</param>
/// <param name="RewardAmount">Размер награды за посещение.</param>
/// <param name="IsActive">Признак активности мероприятия.</param>
public sealed record UpdateEventCommand(
    string? Title,
    string? Description,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    int? MaxParticipants,
    long? RewardAmount,
    bool IsActive);

/// <summary>
/// Команда изменения посещаемости мероприятия.
/// </summary>
/// <param name="Status">Новый статус участника: <c>attended</c> или <c>no_show</c>.</param>
public sealed record EventAttendanceCommand(string? Status);
