using NpgsqlTypes;

public enum TicketStatus
{
    [PgName("open")]
    Open,

    [PgName("in_progress")]
    InProgress,

    [PgName("resolved")]
    Resolved,

    [PgName("closed")]
    Closed
}