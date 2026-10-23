using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexApp.Api.Diagnostics;

/// <summary>
/// Замеряет длительность SQL-команд (значение сообщает EF Core, включая сетевой обмен)
/// и время открытия соединения. Параметры команд не читаются и не логируются.
/// </summary>
internal sealed class SqlTimingInterceptor(bool includeSql) : DbCommandInterceptor, IDbConnectionInterceptor
{
    private const int MaxSqlLength = 120;

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    void IDbConnectionInterceptor.ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        RecordConnectionOpen(eventData);
    }

    Task IDbConnectionInterceptor.ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken)
    {
        RecordConnectionOpen(eventData);
        return Task.CompletedTask;
    }

    private void Record(DbCommand command, CommandExecutedEventData eventData)
    {
        var context = RequestTimingContext.Current;

        if (context is null)
        {
            return;
        }

        var commandKind = DetectCommandKind(command.CommandText);
        context.RecordSql(
            $"sql-{commandKind}",
            eventData.Duration.TotalMilliseconds,
            includeSql ? PrepareSqlForLog(command.CommandText) : null);
    }

    private static void RecordConnectionOpen(ConnectionEndEventData eventData)
    {
        RequestTimingContext.Current?.RecordStage(
            "conn-open",
            eventData.Duration.TotalMilliseconds);
    }

    private static string DetectCommandKind(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "OTHER";
        }

        var text = commandText.AsSpan().TrimStart();
        var end = 0;

        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        var keyword = text[..end].ToString().ToUpperInvariant();

        return keyword switch
        {
            "SELECT" or "WITH" or "INSERT" or "UPDATE" or "DELETE" or "BEGIN" or "START"
                or "COMMIT" or "ROLLBACK" or "SAVEPOINT" or "RELEASE" or "SET" or "SHOW" => keyword,
            _ => "OTHER",
        };
    }

    private static string PrepareSqlForLog(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "-";
        }

        var builder = new StringBuilder(MaxSqlLength);
        var previousWasSpace = false;

        foreach (var symbol in commandText)
        {
            if (char.IsWhiteSpace(symbol) || char.IsControl(symbol))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;

            if (builder.Length >= MaxSqlLength)
            {
                builder.Append("...");
                break;
            }

            builder.Append(symbol);
        }

        return builder.ToString().TrimEnd();
    }
}
