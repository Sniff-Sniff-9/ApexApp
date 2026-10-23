using System.Data;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Сценарии мероприятий: список и детали, регистрация, отмена регистрации,
/// получение награды за посещение, а также администрирование мероприятий
/// и подтверждение посещаемости.
/// </summary>
public sealed class EventService(
    AppDbContext dbContext,
    IActivityRewardService activityRewardService) : IEventService
{
    private const int TitleMaxLength = 200;

    /// <inheritdoc />
    public async Task<PagedResult<EventSummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var now = DateTime.UtcNow;

        var query = dbContext.Events
            .AsNoTracking()
            .Where(eventItem => eventItem.IsActive && eventItem.EndAt >= now);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderBy(eventItem => eventItem.StartAt)
            .ThenBy(eventItem => eventItem.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return new PagedResult<EventSummary>([], totalCount, page, pageSize);
        }

        var eventIds = events.Select(eventItem => eventItem.Id).ToList();

        var registrations = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => participant.UserId == userId
                                  && eventIds.Contains(participant.EventId))
            .ToDictionaryAsync(participant => participant.EventId, cancellationToken);

        var occupiedSeats = await GetOccupiedSeatCountsAsync(eventIds, cancellationToken);

        var items = events
            .Select(eventItem => new EventSummary(
                eventItem,
                occupiedSeats.TryGetValue(eventItem.Id, out var registered) ? registered : 0,
                registrations.TryGetValue(eventItem.Id, out var registration) ? registration : null))
            .ToList();

        return new PagedResult<EventSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<EventDetailsResult>> GetDetailsAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<EventDetailsResult>.Fail(400, "A valid event ID is required.");
        }

        var eventItem = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return ServiceResult<EventDetailsResult>.Fail(404, "Event was not found.");
        }

        var registration = await dbContext.EventParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                participant => participant.EventId == eventId && participant.UserId == userId,
                cancellationToken);

        var isOpen = IsOpenForRegistration(eventItem, DateTime.UtcNow);

        // Как и для квестов, недоступное мероприятие скрывается от пользователя без регистрации,
        // но остаётся доступным участнику, чтобы он видел свой статус и награду.
        if (registration is null && !isOpen)
        {
            return ServiceResult<EventDetailsResult>.Fail(
                409,
                "Event is not active or has already ended.");
        }

        var registeredCount = await CountRegisteredAsync(eventId, cancellationToken);

        return ServiceResult<EventDetailsResult>.Ok(
            new EventDetailsResult(eventItem, registeredCount, registration, isOpen));
    }

    /// <inheritdoc />
    public async Task<PagedResult<EventSummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Events.AsNoTracking();

        if (isActive is bool active)
        {
            query = query.Where(eventItem => eventItem.IsActive == active);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(eventItem => eventItem.StartAt)
            .ThenByDescending(eventItem => eventItem.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return new PagedResult<EventSummary>([], totalCount, page, pageSize);
        }

        var eventIds = events.Select(eventItem => eventItem.Id).ToList();
        var occupiedSeats = await GetOccupiedSeatCountsAsync(eventIds, cancellationToken);

        var items = events
            .Select(eventItem => new EventSummary(
                eventItem,
                occupiedSeats.TryGetValue(eventItem.Id, out var registered) ? registered : 0,
                null))
            .ToList();

        return new PagedResult<EventSummary>(items, totalCount, page, pageSize);
    }


    /// <inheritdoc />
    public async Task<ServiceResult<EventRegistrationResult>> RegisterAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<EventRegistrationResult>.Fail(400, "A valid event ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<EventRegistrationResult>.Fail(400, "A valid user ID is required.");
        }

        var eventItem = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return ServiceResult<EventRegistrationResult>.Fail(404, "Event was not found.");
        }

        if (!IsOpenForRegistration(eventItem, DateTime.UtcNow))
        {
            return ServiceResult<EventRegistrationResult>.Fail(
                409,
                "Event is not active or has already ended.");
        }

        EventParticipant? created = null;

        // Serializable-транзакция защищает проверку свободных мест от гонки:
        // конкурентная регистрация либо сериализуется, либо получает 409.
        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var registration = await dbContext.EventParticipants
                .FirstOrDefaultAsync(
                    participant => participant.EventId == eventId && participant.UserId == userId,
                    cancellationToken);

            if (registration is not null)
            {
                return registration.Status switch
                {
                    EventParticipantStatuses.Registered or EventParticipantStatuses.Attended =>
                        ServiceResult<EventRegistrationResult>.Ok(
                            new EventRegistrationResult(registration, true)),
                    _ => await RestoreRegistrationAsync(
                        registration,
                        eventItem,
                        databaseTransaction,
                        cancellationToken)
                };
            }

            if (!await HasFreeSlotAsync(eventItem, cancellationToken))
            {
                return ServiceResult<EventRegistrationResult>.Fail(409, "Event has no free slots.");
            }

            created = new EventParticipant
            {
                EventId = eventId,
                UserId = userId,
                Status = EventParticipantStatuses.Registered,
                RegisteredAt = DateTime.UtcNow,
                AttendedAt = null,
                RewardClaimed = false
            };

            dbContext.EventParticipants.Add(created);

            await dbContext.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);

            return ServiceResult<EventRegistrationResult>.Ok(
                new EventRegistrationResult(created, false));
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
        {
            // Конкурентный запрос уже создал регистрацию: возвращаем согласованный результат,
            // а не 409, чтобы повторный вызов оставался идемпотентным.
            await databaseTransaction.RollbackAsync(cancellationToken);

            if (created is not null)
            {
                dbContext.Entry(created).State = EntityState.Detached;
            }

            var concurrent = await dbContext.EventParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    participant => participant.EventId == eventId && participant.UserId == userId,
                    cancellationToken);

            if (concurrent is null)
            {
                return ServiceResult<EventRegistrationResult>.Fail(
                    409,
                    "The registration conflicted with another request. Please retry.");
            }

            return concurrent.Status switch
            {
                EventParticipantStatuses.Registered or EventParticipantStatuses.Attended =>
                    ServiceResult<EventRegistrationResult>.Ok(
                        new EventRegistrationResult(concurrent, true)),
                _ => ServiceResult<EventRegistrationResult>.Fail(
                    409,
                    "The registration conflicted with another request. Please retry.")
            };
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            return ServiceResult<EventRegistrationResult>.Fail(
                409,
                "The registration conflicted with another request. Please retry.");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<EventCancellationResult>> CancelRegistrationAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<EventCancellationResult>.Fail(400, "A valid event ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<EventCancellationResult>.Fail(400, "A valid user ID is required.");
        }

        var eventExists = await dbContext.Events
            .AsNoTracking()
            .AnyAsync(item => item.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            return ServiceResult<EventCancellationResult>.Fail(404, "Event was not found.");
        }

        var registration = await dbContext.EventParticipants
            .FirstOrDefaultAsync(
                participant => participant.EventId == eventId && participant.UserId == userId,
                cancellationToken);

        if (registration is null)
        {
            return ServiceResult<EventCancellationResult>.Fail(
                404,
                "You are not registered for this event.");
        }

        if (registration.Status == EventParticipantStatuses.Cancelled)
        {
            return ServiceResult<EventCancellationResult>.Ok(
                new EventCancellationResult(registration, true));
        }

        if (registration.Status == EventParticipantStatuses.Attended)
        {
            return ServiceResult<EventCancellationResult>.Fail(
                409,
                "Attendance is already confirmed and the registration cannot be cancelled.");
        }

        if (registration.Status != EventParticipantStatuses.Registered)
        {
            return ServiceResult<EventCancellationResult>.Fail(
                409,
                "The registration cannot be cancelled.");
        }

        try
        {
            var cancelled = await dbContext.EventParticipants
                .Where(participant => participant.Id == registration.Id
                                      && participant.Status == EventParticipantStatuses.Registered)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        participant => participant.Status,
                        EventParticipantStatuses.Cancelled),
                    cancellationToken);

            if (cancelled == 0)
            {
                var current = await dbContext.EventParticipants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        participant => participant.Id == registration.Id,
                        cancellationToken);

                return current?.Status == EventParticipantStatuses.Cancelled
                    ? ServiceResult<EventCancellationResult>.Ok(
                        new EventCancellationResult(current, true))
                    : ServiceResult<EventCancellationResult>.Fail(
                        409,
                        "The cancellation conflicted with another request. Please retry.");
            }

            registration.Status = EventParticipantStatuses.Cancelled;

            return ServiceResult<EventCancellationResult>.Ok(
                new EventCancellationResult(registration, false));
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception)
                                          || DatabaseErrors.IsUniqueViolation(exception))
        {
            return ServiceResult<EventCancellationResult>.Fail(
                409,
                "The cancellation conflicted with another request. Please retry.");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ActivityRewardResult>> ClaimRewardAsync(
        long eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<ActivityRewardResult>.Fail(400, "A valid event ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<ActivityRewardResult>.Fail(400, "A valid user ID is required.");
        }

        var eventExists = await dbContext.Events
            .AsNoTracking()
            .AnyAsync(item => item.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            return ServiceResult<ActivityRewardResult>.Fail(404, "Event was not found.");
        }

        var registration = await dbContext.EventParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                participant => participant.EventId == eventId && participant.UserId == userId,
                cancellationToken);

        if (registration is null)
        {
            return ServiceResult<ActivityRewardResult>.Fail(
                404,
                "You are not registered for this event.");
        }

        try
        {
            return await activityRewardService.GrantEventRewardAsync(
                registration.Id,
                cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception)
                                          || DatabaseErrors.IsUniqueViolation(exception))
        {
            return ServiceResult<ActivityRewardResult>.Fail(
                409,
                "The reward claim conflicted with another request. Please retry.");
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Event>> CreateAsync(
        CreateEventCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<Event>.Fail(400, "An event command is required.");
        }

        if (createdBy == Guid.Empty)
        {
            return ServiceResult<Event>.Fail(400, "A valid creator ID is required.");
        }

        var validationError = ValidateEventFields(
            command.Title,
            command.StartAt,
            command.EndAt,
            command.MaxParticipants,
            command.RewardAmount);

        if (validationError is not null)
        {
            return ServiceResult<Event>.Fail(400, validationError);
        }

        var now = DateTime.UtcNow;

        var eventItem = new Event
        {
            Title = command.Title!.Trim(),
            Description = NormalizeOptional(command.Description),
            StartAt = ToUtc(command.StartAt!.Value),
            EndAt = ToUtc(command.EndAt!.Value),
            Location = NormalizeOptional(command.Location),
            MaxParticipants = command.MaxParticipants,
            RewardAmount = command.RewardAmount!.Value,
            IsActive = command.IsActive,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Events.Add(eventItem);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            dbContext.Entry(eventItem).State = EntityState.Detached;

            return ServiceResult<Event>.Fail(400, "The event creator was not found.");
        }

        return ServiceResult<Event>.Ok(eventItem);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Event>> UpdateAsync(
        long eventId,
        UpdateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<Event>.Fail(400, "A valid event ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<Event>.Fail(400, "An event command is required.");
        }

        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return ServiceResult<Event>.Fail(404, "Event was not found.");
        }

        var validationError = ValidateEventFields(
            command.Title,
            command.StartAt,
            command.EndAt,
            command.MaxParticipants,
            command.RewardAmount);

        if (validationError is not null)
        {
            return ServiceResult<Event>.Fail(400, validationError);
        }

        eventItem.Title = command.Title!.Trim();
        eventItem.Description = NormalizeOptional(command.Description);
        eventItem.StartAt = ToUtc(command.StartAt!.Value);
        eventItem.EndAt = ToUtc(command.EndAt!.Value);
        eventItem.Location = NormalizeOptional(command.Location);
        eventItem.MaxParticipants = command.MaxParticipants;
        eventItem.RewardAmount = command.RewardAmount!.Value;
        eventItem.IsActive = command.IsActive;
        eventItem.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Event>.Ok(eventItem);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Event>> DeactivateAsync(
        long eventId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<Event>.Fail(400, "A valid event ID is required.");
        }

        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return ServiceResult<Event>.Fail(404, "Event was not found.");
        }

        if (eventItem.IsActive)
        {
            // Мероприятие не удаляется физически: регистрации и награды остаются
            // частью истории, поэтому удаление сводится к деактивации.
            eventItem.IsActive = false;
            eventItem.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<Event>.Ok(eventItem);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PagedResult<EventParticipant>>> GetParticipantsAsync(
        long eventId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return ServiceResult<PagedResult<EventParticipant>>.Fail(
                400,
                "A valid event ID is required.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var eventExists = await dbContext.Events
            .AsNoTracking()
            .AnyAsync(item => item.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            return ServiceResult<PagedResult<EventParticipant>>.Fail(404, "Event was not found.");
        }

        var query = dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => participant.EventId == eventId);

        var totalCount = await query.CountAsync(cancellationToken);

        var participants = await query
            .OrderByDescending(participant => participant.RegisteredAt)
            .ThenByDescending(participant => participant.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(participant => participant.User)
            .ToListAsync(cancellationToken);

        return ServiceResult<PagedResult<EventParticipant>>.Ok(
            new PagedResult<EventParticipant>(participants, totalCount, page, pageSize));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<EventAttendanceResult>> SetAttendanceAsync(
        long eventId,
        long participantId,
        EventAttendanceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0 || participantId <= 0)
        {
            return ServiceResult<EventAttendanceResult>.Fail(
                400,
                "Valid event and participant IDs are required.");
        }

        if (command is null)
        {
            return ServiceResult<EventAttendanceResult>.Fail(
                400,
                "An attendance command is required.");
        }

        var targetStatus = command.Status?.Trim().ToLowerInvariant();

        if (targetStatus is not (EventParticipantStatuses.Attended or EventParticipantStatuses.NoShow))
        {
            return ServiceResult<EventAttendanceResult>.Fail(
                400,
                "Status must be either \"attended\" or \"no_show\".");
        }

        var participant = await dbContext.EventParticipants
            .FirstOrDefaultAsync(
                item => item.Id == participantId && item.EventId == eventId,
                cancellationToken);

        if (participant is null)
        {
            return ServiceResult<EventAttendanceResult>.Fail(
                404,
                "Event participant was not found.");
        }

        if (participant.Status == targetStatus)
        {
            return ServiceResult<EventAttendanceResult>.Ok(
                new EventAttendanceResult(participant, true));
        }

        try
        {
            if (targetStatus == EventParticipantStatuses.NoShow)
            {
                if (participant.RewardClaimed)
                {
                    return ServiceResult<EventAttendanceResult>.Fail(
                        409,
                        "The reward was already granted, so attendance cannot be changed.");
                }

                if (participant.Status == EventParticipantStatuses.Cancelled)
                {
                    return ServiceResult<EventAttendanceResult>.Fail(
                        409,
                        "A cancelled registration cannot be marked as no_show.");
                }

                // Условное обновление не позволяет снять посещаемость, если награда
                // была захвачена конкурентным запросом.
                var cleared = await dbContext.EventParticipants
                    .Where(item => item.Id == participant.Id
                                   && !item.RewardClaimed
                                   && item.Status != EventParticipantStatuses.Cancelled)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.Status, EventParticipantStatuses.NoShow)
                            .SetProperty(item => item.AttendedAt, (DateTime?)null),
                        cancellationToken);

                if (cleared == 0)
                {
                    return ServiceResult<EventAttendanceResult>.Fail(
                        409,
                        "The attendance update conflicted with another request. Please retry.");
                }

                participant.Status = EventParticipantStatuses.NoShow;
                participant.AttendedAt = null;

                return ServiceResult<EventAttendanceResult>.Ok(
                    new EventAttendanceResult(participant, false));
            }

            if (participant.Status == EventParticipantStatuses.Cancelled)
            {
                return ServiceResult<EventAttendanceResult>.Fail(
                    409,
                    "A cancelled registration cannot be marked as attended.");
            }

            var attendedAt = participant.AttendedAt ?? DateTime.UtcNow;

            var updated = await dbContext.EventParticipants
                .Where(item => item.Id == participant.Id
                               && item.Status != EventParticipantStatuses.Cancelled)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, EventParticipantStatuses.Attended)
                        .SetProperty(item => item.AttendedAt, (DateTime?)attendedAt),
                    cancellationToken);

            if (updated == 0)
            {
                return ServiceResult<EventAttendanceResult>.Fail(
                    409,
                    "The attendance update conflicted with another request. Please retry.");
            }

            participant.Status = EventParticipantStatuses.Attended;
            participant.AttendedAt = attendedAt;

            return ServiceResult<EventAttendanceResult>.Ok(
                new EventAttendanceResult(participant, false));
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            return ServiceResult<EventAttendanceResult>.Fail(
                409,
                "The attendance update conflicted with another request. Please retry.");
        }
    }

    /// <summary>
    /// Восстанавливает отменённую регистрацию в рамках уже открытой транзакции.
    /// </summary>
    private async Task<ServiceResult<EventRegistrationResult>> RestoreRegistrationAsync(
        EventParticipant registration,
        Event eventItem,
        IDbContextTransaction databaseTransaction,
        CancellationToken cancellationToken)
    {
        if (registration.RewardClaimed)
        {
            return ServiceResult<EventRegistrationResult>.Fail(
                409,
                "The registration cannot be restored because the reward was already granted.");
        }

        if (!await HasFreeSlotAsync(eventItem, cancellationToken))
        {
            return ServiceResult<EventRegistrationResult>.Fail(409, "Event has no free slots.");
        }

        registration.Status = EventParticipantStatuses.Registered;
        registration.RegisteredAt = DateTime.UtcNow;
        registration.AttendedAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return ServiceResult<EventRegistrationResult>.Ok(
            new EventRegistrationResult(registration, false));
    }

    /// <summary>
    /// Проверяет наличие свободного места с учётом лимита участников.
    /// </summary>
    private async Task<bool> HasFreeSlotAsync(Event eventItem, CancellationToken cancellationToken)
    {
        if (eventItem.MaxParticipants is not int limit)
        {
            return true;
        }

        if (limit <= 0)
        {
            return false;
        }

        return await CountRegisteredAsync(eventItem.Id, cancellationToken) < limit;
    }

    /// <summary>
    /// Считает занятые места для набора мероприятий одним запросом.
    /// </summary>
    private async Task<Dictionary<long, int>> GetOccupiedSeatCountsAsync(
        IReadOnlyCollection<long> eventIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.EventParticipants
            .AsNoTracking()
            .Where(participant => eventIds.Contains(participant.EventId)
                                  && (participant.Status == EventParticipantStatuses.Registered
                                      || participant.Status == EventParticipantStatuses.Attended))
            .GroupBy(participant => participant.EventId)
            .Select(group => new { EventId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.EventId, row => row.Count);
    }

    /// <summary>
    /// Считает участников, занимающих место: зарегистрированных и посетивших.
    /// </summary>
    private Task<int> CountRegisteredAsync(long eventId, CancellationToken cancellationToken) =>
        dbContext.EventParticipants.CountAsync(
            participant => participant.EventId == eventId
                            && (participant.Status == EventParticipantStatuses.Registered
                                || participant.Status == EventParticipantStatuses.Attended),
            cancellationToken);

    /// <summary>
    /// Регистрация доступна для активного мероприятия, которое ещё не закончилось;
    /// до начала мероприятия регистрация разрешена.
    /// </summary>
    private static bool IsOpenForRegistration(Event eventItem, DateTime now) =>
        eventItem.IsActive && eventItem.EndAt >= now;

    /// <summary>
    /// Проверяет поля мероприятия и возвращает сообщение об ошибке или <c>null</c>.
    /// </summary>
    private static string? ValidateEventFields(
        string? title,
        DateTime? startAt,
        DateTime? endAt,
        int? maxParticipants,
        long? rewardAmount)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }

        if (title.Trim().Length > TitleMaxLength)
        {
            return $"Title must not exceed {TitleMaxLength} characters.";
        }

        if (startAt is null || endAt is null)
        {
            return "StartAt and EndAt are required.";
        }

        if (endAt <= startAt)
        {
            return "EndAt must be later than StartAt.";
        }

        if (maxParticipants is <= 0)
        {
            return "MaxParticipants must be greater than zero.";
        }

        if (rewardAmount is null)
        {
            return "RewardAmount is required.";
        }

        if (rewardAmount < 0)
        {
            return "RewardAmount cannot be negative.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Приводит дату к UTC, чтобы значение можно было записать в <c>timestamptz</c>.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
