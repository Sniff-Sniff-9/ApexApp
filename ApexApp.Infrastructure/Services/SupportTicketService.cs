using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Auth;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Обращения в поддержку: создание обращений, переписка, просмотр собственных обращений,
/// а также управление обращениями со стороны сотрудников поддержки.
/// </summary>
public sealed class SupportTicketService(AppDbContext dbContext) : ISupportTicketService
{
    private const int SubjectMaxLength = 255;
    private const int MessageMaxLength = 4000;

    private static readonly string[] AssigneeRoleCodes =
    [
        RoleCodes.Support,
        RoleCodes.Admin,
        RoleCodes.SuperAdmin
    ];

    /// <inheritdoc />
    public async Task<ServiceResult<SupportTicketDetails>> CreateAsync(
        Guid userId,
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<SupportTicketDetails>.Fail(400, "A valid user ID is required.");
        }

        var subject = command.Subject?.Trim();
        var message = command.Message?.Trim();

        if (string.IsNullOrWhiteSpace(subject))
        {
            return ServiceResult<SupportTicketDetails>.Fail(400, "Subject is required.");
        }

        if (subject.Length > SubjectMaxLength)
        {
            return ServiceResult<SupportTicketDetails>.Fail(
                400,
                $"Subject must not exceed {SubjectMaxLength} characters.");
        }

        var messageError = ValidateMessage(message);

        if (messageError is not null)
        {
            return ServiceResult<SupportTicketDetails>.Fail(400, messageError);
        }

        var now = DateTime.UtcNow;

        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = subject,
            Status = TicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        ticket.SupportMessages.Add(new SupportMessage
        {
            UserId = userId,
            Message = message!,
            CreatedAt = now
        });

        dbContext.SupportTickets.Add(ticket);

        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await LoadDetailsAsync(ticket.Id, ownerId: null, cancellationToken);

        return details is null
            ? ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.")
            : ServiceResult<SupportTicketDetails>.Ok(details);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SupportTicketSummary>> GetMyAsync(
        Guid userId,
        TicketStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId);

        if (status is not null)
        {
            query = query.Where(ticket => ticket.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectSummaries(query)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SupportTicketSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SupportTicketSummary>> GetAllAsync(
        TicketStatus? status,
        Guid? assignedTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.SupportTickets.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(ticket => ticket.Status == status.Value);
        }

        if (assignedTo is not null)
        {
            query = query.Where(ticket => ticket.AssignedTo == assignedTo);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectSummaries(query)
            .OrderByDescending(ticket => ticket.UpdatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SupportTicketSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SupportTicketDetails>> GetByIdAsync(
        Guid userId,
        bool canManage,
        long ticketId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return ServiceResult<SupportTicketDetails>.Fail(
                400,
                "A valid support ticket ID is required.");
        }

        var ownerId = canManage ? (Guid?)null : userId;

        var details = await LoadDetailsAsync(ticketId, ownerId, cancellationToken);

        return details is null
            ? ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.")
            : ServiceResult<SupportTicketDetails>.Ok(details);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SupportMessageItem>> AddMessageAsync(
        Guid userId,
        bool canManage,
        long ticketId,
        SendSupportMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<SupportMessageItem>.Fail(400, "A valid user ID is required.");
        }

        if (ticketId <= 0)
        {
            return ServiceResult<SupportMessageItem>.Fail(
                400,
                "A valid support ticket ID is required.");
        }

        var message = command.Message?.Trim();

        var messageError = ValidateMessage(message);

        if (messageError is not null)
        {
            return ServiceResult<SupportMessageItem>.Fail(400, messageError);
        }

        var ticket = await dbContext.SupportTickets
            .FirstOrDefaultAsync(
                ticket => ticket.Id == ticketId
                          && (canManage || ticket.UserId == userId),
                cancellationToken);

        if (ticket is null)
        {
            return ServiceResult<SupportMessageItem>.Fail(404, "Support ticket was not found.");
        }

        var now = DateTime.UtcNow;

        var entity = new SupportMessage
        {
            TicketId = ticket.Id,
            UserId = userId,
            Message = message!,
            CreatedAt = now
        };

        dbContext.SupportMessages.Add(entity);

        ticket.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var author = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.FirstName,
                user.LastName,
                user.MiddleName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<SupportMessageItem>.Ok(new SupportMessageItem(
            entity.Id,
            entity.UserId,
            author?.FirstName ?? string.Empty,
            author?.LastName ?? string.Empty,
            author?.MiddleName,
            entity.Message,
            entity.CreatedAt));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SupportTicketDetails>> UpdateStatusAsync(
        long ticketId,
        TicketStatus status,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return ServiceResult<SupportTicketDetails>.Fail(
                400,
                "A valid support ticket ID is required.");
        }

        if (!Enum.IsDefined(status))
        {
            return ServiceResult<SupportTicketDetails>.Fail(400, "Invalid ticket status.");
        }

        var ticket = await dbContext.SupportTickets
            .FirstOrDefaultAsync(
                ticket => ticket.Id == ticketId,
                cancellationToken);

        if (ticket is null)
        {
            return ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.");
        }

        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await LoadDetailsAsync(ticket.Id, ownerId: null, cancellationToken);

        return details is null
            ? ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.")
            : ServiceResult<SupportTicketDetails>.Ok(details);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SupportTicketDetails>> AssignAsync(
        long ticketId,
        Guid? assigneeId,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            return ServiceResult<SupportTicketDetails>.Fail(
                400,
                "A valid support ticket ID is required.");
        }

        if (assigneeId == Guid.Empty)
        {
            return ServiceResult<SupportTicketDetails>.Fail(
                400,
                "A valid assignee ID is required; use null to unassign the ticket.");
        }

        if (assigneeId is not null)
        {
            var assigneeGuid = assigneeId.Value;

            var isActive = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == assigneeGuid)
                .Select(user => (bool?)user.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (isActive is null)
            {
                return ServiceResult<SupportTicketDetails>.Fail(404, "Assignee was not found.");
            }

            if (!isActive.Value)
            {
                return ServiceResult<SupportTicketDetails>.Fail(
                    400,
                    "Assignee must be an active user.");
            }

            var isSupportStaff = await dbContext.UserRoles
                .AsNoTracking()
                .AnyAsync(
                    userRole => userRole.UserId == assigneeGuid
                                && AssigneeRoleCodes.Contains(userRole.Role.Code),
                    cancellationToken);

            if (!isSupportStaff)
            {
                return ServiceResult<SupportTicketDetails>.Fail(
                    400,
                    "Assignee must be a member of the support staff.");
            }
        }

        var ticket = await dbContext.SupportTickets
            .FirstOrDefaultAsync(
                ticket => ticket.Id == ticketId,
                cancellationToken);

        if (ticket is null)
        {
            return ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.");
        }

        ticket.AssignedTo = assigneeId;
        ticket.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await LoadDetailsAsync(ticket.Id, ownerId: null, cancellationToken);

        return details is null
            ? ServiceResult<SupportTicketDetails>.Fail(404, "Support ticket was not found.")
            : ServiceResult<SupportTicketDetails>.Ok(details);
    }


    /// <summary>
    /// Загружает обращение вместе с историей сообщений одним запросом.
    /// Если задан <paramref name="ownerId"/>, обращение доступно только его автору.
    /// </summary>
    /// <param name="ticketId">Идентификатор обращения.</param>
    /// <param name="ownerId">Идентификатор автора обращения или <c>null</c>, если доступ не ограничен.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task<SupportTicketDetails?> LoadDetailsAsync(
        long ticketId,
        Guid? ownerId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);

        if (ownerId is not null)
        {
            query = query.Where(ticket => ticket.UserId == ownerId);
        }

        return await query
            .Select(ticket => new SupportTicketDetails(
                ticket.Id,
                ticket.Subject,
                ticket.Status,
                ticket.UserId,
                ticket.User.FirstName,
                ticket.User.LastName,
                ticket.User.MiddleName,
                ticket.AssignedTo,
                ticket.AssignedTo == null
                    ? null
                    : ticket.AssignedToNavigation!.FirstName,
                ticket.AssignedTo == null
                    ? null
                    : ticket.AssignedToNavigation!.LastName,
                ticket.AssignedTo == null
                    ? null
                    : ticket.AssignedToNavigation!.MiddleName,
                ticket.CreatedAt,
                ticket.UpdatedAt,
                ticket.SupportMessages
                    .OrderBy(message => message.CreatedAt)
                    .ThenBy(message => message.Id)
                    .Select(message => new SupportMessageItem(
                        message.Id,
                        message.UserId,
                        message.User.FirstName,
                        message.User.LastName,
                        message.User.MiddleName,
                        message.Message,
                        message.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Проецирует обращения в краткую информацию для списков,
    /// вычисляя количество и дату последнего сообщения на стороне базы данных.
    /// </summary>
    /// <param name="tickets">Запрос обращений.</param>
    private static IQueryable<SupportTicketSummary> ProjectSummaries(
        IQueryable<SupportTicket> tickets) =>
        tickets.Select(ticket => new SupportTicketSummary(
            ticket.Id,
            ticket.Subject,
            ticket.Status,
            ticket.UserId,
            ticket.User.FirstName,
            ticket.User.LastName,
            ticket.User.MiddleName,
            ticket.AssignedTo,
            ticket.AssignedTo == null
                ? null
                : ticket.AssignedToNavigation!.FirstName,
            ticket.AssignedTo == null
                ? null
                : ticket.AssignedToNavigation!.LastName,
            ticket.AssignedTo == null
                ? null
                : ticket.AssignedToNavigation!.MiddleName,
            ticket.SupportMessages.Count,
            ticket.SupportMessages
                .Select(message => (DateTime?)message.CreatedAt)
                .Max(),
            ticket.CreatedAt,
            ticket.UpdatedAt));

    /// <summary>
    /// Проверяет текст сообщения обращения.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <returns>Сообщение об ошибке или <c>null</c>, если текст корректен.</returns>
    private static string? ValidateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Message is required.";
        }

        return message.Length > MessageMaxLength
            ? $"Message must not exceed {MessageMaxLength} characters."
            : null;
    }
}

