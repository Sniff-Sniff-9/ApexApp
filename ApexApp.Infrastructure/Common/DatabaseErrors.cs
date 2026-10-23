using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Распознаёт ошибки PostgreSQL, которые влияют на обработку конкурентных запросов
/// и нарушений ограничений БД.
/// </summary>
public static class DatabaseErrors
{
    /// <summary>
    /// Признак конфликта, который можно повторить: ошибка сериализации,
    /// взаимоблокировка или недоступная блокировка.
    /// </summary>
    /// <param name="exception">Исключение, полученное при работе с БД.</param>
    public static bool IsRetryableConflict(Exception exception) =>
        exception switch
        {
            DbUpdateException updateException =>
                updateException.InnerException is not null
                && IsRetryableConflict(updateException.InnerException),
            PostgresException postgresException => postgresException.SqlState
                is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected
                or PostgresErrorCodes.LockNotAvailable,
            _ => false
        };

    /// <summary>
    /// Признак нарушения уникального ограничения (<c>23505</c>).
    /// </summary>
    /// <param name="exception">Исключение, полученное при работе с БД.</param>
    public static bool IsUniqueViolation(Exception exception) =>
        exception switch
        {
            DbUpdateException updateException =>
                updateException.InnerException is not null
                && IsUniqueViolation(updateException.InnerException),
            PostgresException postgresException =>
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation,
            _ => false
        };

    /// <summary>
    /// Признак нарушения внешнего ключа (<c>23503</c>).
    /// </summary>
    /// <param name="exception">Исключение, полученное при работе с БД.</param>
    public static bool IsForeignKeyViolation(Exception exception) =>
        exception switch
        {
            DbUpdateException updateException =>
                updateException.InnerException is not null
                && IsForeignKeyViolation(updateException.InnerException),
            PostgresException postgresException =>
                postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation,
            _ => false
        };
}
