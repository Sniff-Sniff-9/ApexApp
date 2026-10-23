using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Сценарии мероприятий: просмотр, регистрация, отмена регистрации и получение награды,
/// а также администрирование мероприятий и подтверждение посещаемости.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Возвращает страницу активных мероприятий, которые ещё не закончились,
    /// вместе с регистрацией пользователя и количеством участников.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<EventSummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает детали мероприятия вместе с регистрацией пользователя.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<EventDetailsResult>> GetDetailsAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Регистрирует пользователя на мероприятие.
    /// Повторный запрос возвращает действующую регистрацию без создания дубликата;
    /// отменённая регистрация восстанавливается, если есть свободные места.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<EventRegistrationResult>> RegisterAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отменяет регистрацию пользователя на мероприятие.
    /// Повторный запрос идемпотентен.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<EventCancellationResult>> CancelRegistrationAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Начисляет награду за посещённое мероприятие.
    /// Повторный вызов не начисляет награду повторно.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<ActivityRewardResult>> ClaimRewardAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу мероприятий для административного просмотра,
    /// включая неактивные и завершившиеся.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех мероприятий.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<EventSummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт мероприятие.
    /// </summary>
    /// <param name="command">Данные мероприятия.</param>
    /// <param name="createdBy">Идентификатор создателя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Event>> CreateAsync(
        CreateEventCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет мероприятие.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="command">Данные мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Event>> UpdateAsync(
        long eventId,
        UpdateEventCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Деактивирует мероприятие, сохраняя регистрации и историю наград.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Event>> DeactivateAsync(
        long eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу участников мероприятия.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<PagedResult<EventParticipant>>> GetParticipantsAsync(
        long eventId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет посещаемость участника мероприятия.
    /// </summary>
    /// <param name="eventId">Идентификатор мероприятия.</param>
    /// <param name="participantId">Идентификатор регистрации.</param>
    /// <param name="command">Новый статус: <c>attended</c> или <c>no_show</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<EventAttendanceResult>> SetAttendanceAsync(
        long eventId,
        long participantId,
        EventAttendanceCommand command,
        CancellationToken cancellationToken = default);
}
