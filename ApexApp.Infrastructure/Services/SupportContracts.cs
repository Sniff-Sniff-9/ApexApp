namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Краткая информация об обращении в поддержку для списков.
/// </summary>
/// <param name="Id">Идентификатор обращения.</param>
/// <param name="Subject">Тема обращения.</param>
/// <param name="Status">Текущий статус обращения.</param>
/// <param name="UserId">Идентификатор автора обращения.</param>
/// <param name="UserFirstName">Имя автора обращения.</param>
/// <param name="UserLastName">Фамилия автора обращения.</param>
/// <param name="UserMiddleName">Отчество автора обращения.</param>
/// <param name="AssignedTo">Идентификатор назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToFirstName">Имя назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToLastName">Фамилия назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToMiddleName">Отчество назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="MessagesCount">Количество сообщений в обращении.</param>
/// <param name="LastMessageAt">Дата и время последнего сообщения или <c>null</c>, если сообщений нет.</param>
/// <param name="CreatedAt">Дата и время создания обращения.</param>
/// <param name="UpdatedAt">Дата и время последнего изменения обращения.</param>
public sealed record SupportTicketSummary(
    long Id,
    string Subject,
    TicketStatus Status,
    Guid UserId,
    string UserFirstName,
    string UserLastName,
    string? UserMiddleName,
    Guid? AssignedTo,
    string? AssignedToFirstName,
    string? AssignedToLastName,
    string? AssignedToMiddleName,
    int MessagesCount,
    DateTime? LastMessageAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Полное имя автора обращения.
    /// </summary>
    public string UserName => SupportNames.Format(UserFirstName, UserLastName, UserMiddleName);

    /// <summary>
    /// Полное имя назначенного сотрудника поддержки или <c>null</c>.
    /// </summary>
    public string? AssignedToName => AssignedToFirstName is null
        ? null
        : SupportNames.Format(
            AssignedToFirstName,
            AssignedToLastName ?? string.Empty,
            AssignedToMiddleName);
}

/// <summary>
/// Сообщение внутри обращения в поддержку.
/// </summary>
/// <param name="Id">Идентификатор сообщения.</param>
/// <param name="UserId">Идентификатор автора сообщения.</param>
/// <param name="UserFirstName">Имя автора сообщения.</param>
/// <param name="UserLastName">Фамилия автора сообщения.</param>
/// <param name="UserMiddleName">Отчество автора сообщения.</param>
/// <param name="Message">Текст сообщения.</param>
/// <param name="CreatedAt">Дата и время создания сообщения.</param>
public sealed record SupportMessageItem(
    long Id,
    Guid UserId,
    string UserFirstName,
    string UserLastName,
    string? UserMiddleName,
    string Message,
    DateTime CreatedAt)
{
    /// <summary>
    /// Полное имя автора сообщения.
    /// </summary>
    public string UserName => SupportNames.Format(UserFirstName, UserLastName, UserMiddleName);
}

/// <summary>
/// Обращение в поддержку вместе с историей сообщений.
/// </summary>
/// <param name="Id">Идентификатор обращения.</param>
/// <param name="Subject">Тема обращения.</param>
/// <param name="Status">Текущий статус обращения.</param>
/// <param name="UserId">Идентификатор автора обращения.</param>
/// <param name="UserFirstName">Имя автора обращения.</param>
/// <param name="UserLastName">Фамилия автора обращения.</param>
/// <param name="UserMiddleName">Отчество автора обращения.</param>
/// <param name="AssignedTo">Идентификатор назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToFirstName">Имя назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToLastName">Фамилия назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="AssignedToMiddleName">Отчество назначенного сотрудника поддержки или <c>null</c>.</param>
/// <param name="CreatedAt">Дата и время создания обращения.</param>
/// <param name="UpdatedAt">Дата и время последнего изменения обращения.</param>
/// <param name="Messages">Сообщения обращения, упорядоченные по дате создания.</param>
public sealed record SupportTicketDetails(
    long Id,
    string Subject,
    TicketStatus Status,
    Guid UserId,
    string UserFirstName,
    string UserLastName,
    string? UserMiddleName,
    Guid? AssignedTo,
    string? AssignedToFirstName,
    string? AssignedToLastName,
    string? AssignedToMiddleName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<SupportMessageItem> Messages)
{
    /// <summary>
    /// Полное имя автора обращения.
    /// </summary>
    public string UserName => SupportNames.Format(UserFirstName, UserLastName, UserMiddleName);

    /// <summary>
    /// Полное имя назначенного сотрудника поддержки или <c>null</c>.
    /// </summary>
    public string? AssignedToName => AssignedToFirstName is null
        ? null
        : SupportNames.Format(
            AssignedToFirstName,
            AssignedToLastName ?? string.Empty,
            AssignedToMiddleName);
}

/// <summary>
/// Данные для создания обращения в поддержку.
/// </summary>
/// <param name="Subject">Тема обращения (обязательна, до 255 символов).</param>
/// <param name="Message">Первое сообщение обращения (обязательно, до 4000 символов).</param>
public sealed record CreateSupportTicketCommand(string? Subject, string? Message);

/// <summary>
/// Данные для отправки сообщения в обращение.
/// </summary>
/// <param name="Message">Текст сообщения (обязателен, до 4000 символов).</param>
public sealed record SendSupportMessageCommand(string? Message);

/// <summary>
/// Форматирование полных имён пользователей для контрактов поддержки.
/// </summary>
internal static class SupportNames
{
    /// <summary>
    /// Собирает полное имя из имени, фамилии и отчества.
    /// </summary>
    /// <param name="firstName">Имя.</param>
    /// <param name="lastName">Фамилия.</param>
    /// <param name="middleName">Отчество; может отсутствовать.</param>
    public static string Format(string firstName, string lastName, string? middleName) =>
        string.IsNullOrWhiteSpace(middleName)
            ? $"{firstName} {lastName}"
            : $"{firstName} {middleName} {lastName}";
}

