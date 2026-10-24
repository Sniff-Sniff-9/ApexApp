namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Запрос на создание обращения в поддержку.
    /// </summary>
    /// <param name="Subject">Тема обращения (обязательна, до 255 символов).</param>
    /// <param name="Message">Текст обращения (обязателен, до 4000 символов).</param>
    public sealed record CreateSupportTicketRequest(
        string? Subject,
        string? Message);

    /// <summary>
    /// Запрос на отправку сообщения в обращение.
    /// </summary>
    /// <param name="Message">Текст сообщения (обязателен, до 4000 символов).</param>
    public sealed record SendSupportMessageRequest(
        string? Message);

    /// <summary>
    /// Запрос на изменение статуса обращения.
    /// </summary>
    /// <param name="Status">
    /// Новый статус: <c>Open</c>, <c>InProgress</c>, <c>Resolved</c> или <c>Closed</c>.
    /// Допускаются значения в любом регистре и в формате базы данных (<c>in_progress</c>).
    /// </param>
    public sealed record UpdateSupportTicketStatusRequest(
        string? Status);

    /// <summary>
    /// Запрос на назначение обращения сотруднику поддержки.
    /// </summary>
    /// <param name="UserId">Идентификатор сотрудника поддержки или <c>null</c> для снятия назначения.</param>
    public sealed record AssignSupportTicketRequest(
        Guid? UserId);
}
