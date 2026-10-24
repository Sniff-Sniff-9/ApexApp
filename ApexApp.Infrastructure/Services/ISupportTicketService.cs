using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Обращения в поддержку: создание обращений, переписка в обращении, просмотр
/// собственных обращений, а также управление обращениями со стороны сотрудников поддержки.
/// </summary>
public interface ISupportTicketService
{
    /// <summary>
    /// Создаёт обращение вместе с первым сообщением в одной транзакции.
    /// Автор обращения определяется по идентификатору текущего пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор автора обращения.</param>
    /// <param name="command">Тема обращения и текст первого сообщения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SupportTicketDetails>> CreateAsync(
        Guid userId,
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу обращений текущего пользователя, начиная с самых новых.
    /// </summary>
    /// <param name="userId">Идентификатор текущего пользователя.</param>
    /// <param name="status">Необязательный фильтр по статусу.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<SupportTicketSummary>> GetMyAsync(
        Guid userId,
        TicketStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу всех обращений для сотрудников поддержки,
    /// начиная с последних изменённых.
    /// </summary>
    /// <param name="status">Необязательный фильтр по статусу.</param>
    /// <param name="assignedTo">Необязательный фильтр по назначенному сотруднику.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<SupportTicketSummary>> GetAllAsync(
        TicketStatus? status,
        Guid? assignedTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает обращение вместе с историей сообщений.
    /// Обращение доступно его автору, а сотрудникам поддержки — любое обращение.
    /// </summary>
    /// <param name="userId">Идентификатор текущего пользователя.</param>
    /// <param name="canManage">Признак того, что пользователь управляет обращениями.</param>
    /// <param name="ticketId">Идентификатор обращения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SupportTicketDetails>> GetByIdAsync(
        Guid userId,
        bool canManage,
        long ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет сообщение в обращение. Автор сообщения определяется
    /// по идентификатору текущего пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор автора сообщения.</param>
    /// <param name="canManage">Признак того, что пользователь управляет обращениями.</param>
    /// <param name="ticketId">Идентификатор обращения.</param>
    /// <param name="command">Текст сообщения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SupportMessageItem>> AddMessageAsync(
        Guid userId,
        bool canManage,
        long ticketId,
        SendSupportMessageCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет статус обращения.
    /// </summary>
    /// <param name="ticketId">Идентификатор обращения.</param>
    /// <param name="status">Новый статус обращения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SupportTicketDetails>> UpdateStatusAsync(
        long ticketId,
        TicketStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Назначает обращение сотруднику поддержки; <c>null</c> снимает назначение.
    /// </summary>
    /// <param name="ticketId">Идентификатор обращения.</param>
    /// <param name="assigneeId">Идентификатор сотрудника поддержки или <c>null</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SupportTicketDetails>> AssignAsync(
        long ticketId,
        Guid? assigneeId,
        CancellationToken cancellationToken = default);
}
