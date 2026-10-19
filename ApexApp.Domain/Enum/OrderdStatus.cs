using NpgsqlTypes;

public enum OrderStatus
{
    [PgName("pending")]
    Pending,

    [PgName("paid")]
    Paid,

    [PgName("processing")]
    Processing,

    [PgName("ready")]
    Ready,

    [PgName("delivered")]
    Delivered,

    [PgName("cancelled")]
    Cancelled,

    [PgName("refunded")]
    Refunded
}