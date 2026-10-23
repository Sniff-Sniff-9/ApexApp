using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ApexApp.Api.Diagnostics;

/// <summary>
/// Собирает тайминги этапов обработки одного HTTP-запроса в рамках асинхронного потока.
/// Используется только в Development и только при включённом <c>Diagnostics:Timing:Enabled</c>.
/// Не содержит бизнес-логики и не влияет на ответы API.
/// </summary>
internal sealed class RequestTimingContext
{
    private const int MaxStages = 64;

    private static readonly AsyncLocal<RequestTimingContext?> Holder = new();

    private readonly List<RequestStage> _stages = new(MaxStages);
    private readonly Lock _gate = new();
    private readonly long _startedAt = Stopwatch.GetTimestamp();

    /// <summary>Текущий контекст запроса или <c>null</c>, если диагностика выключена.</summary>
    public static RequestTimingContext? Current => Holder.Value;

    /// <summary>Полное время с начала обработки запроса.</summary>
    public double TotalMs => Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;

    /// <summary>Количество выполненных SQL-команд.</summary>
    public int SqlCount { get; private set; }

    /// <summary>Суммарная длительность SQL-команд в миллисекундах.</summary>
    public double SqlTotalMs { get; private set; }

    /// <summary>Снимок зафиксированных этапов.</summary>
    public IReadOnlyList<RequestStage> Stages
    {
        get
        {
            lock (_gate)
            {
                return _stages.ToArray();
            }
        }
    }

    /// <summary>Создаёт и делает текущим контекст нового запроса.</summary>
    public static RequestTimingContext Begin()
    {
        var context = new RequestTimingContext();
        Holder.Value = context;
        return context;
    }

    /// <summary>Очищает контекст, чтобы он не «протекал» между запросами.</summary>
    public static void End() => Holder.Value = null;

    /// <summary>
    /// Открывает именованный этап. Возвращает <c>null</c>, если диагностика не активна,
    /// поэтому вызов безопасен и в Production.
    /// </summary>
    public static IDisposable? Stage(string name)
    {
        var context = Holder.Value;
        return context is null ? null : context.StartStage(name);
    }

    /// <summary>Регистрирует SQL-команду с длительностью, измеренной EF Core.</summary>
    public void RecordSql(string name, double durationMs, string? detail = null)
    {
        lock (_gate)
        {
            SqlCount++;
            SqlTotalMs += durationMs;
            AddCore(name, durationMs, detail);
        }
    }

    /// <summary>Регистрирует длительность этапа.</summary>
    public void RecordStage(string name, double durationMs)
    {
        lock (_gate)
        {
            AddCore(name, durationMs, null);
        }
    }

    /// <summary>Формирует значение заголовка <c>Server-Timing</c> (имя этапа + длительность).</summary>
    public string ToServerTiming(double totalMs)
    {
        var stages = Stages;
        var parts = new List<string>(stages.Count + 1);

        foreach (var stage in stages)
        {
            parts.Add(
                $"{Sanitize(stage.Name)};dur={stage.DurationMs.ToString("F1", CultureInfo.InvariantCulture)}");
        }

        parts.Add($"total;dur={totalMs.ToString("F1", CultureInfo.InvariantCulture)}");

        return string.Join(", ", parts);
    }

    /// <summary>Формирует компактное описание этапов для строки лога.</summary>
    public string DescribeStages()
    {
        var stages = Stages;

        if (stages.Count == 0)
        {
            return "-";
        }

        var builder = new StringBuilder(stages.Count * 24);

        foreach (var stage in stages)
        {
            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(stage.Name)
                .Append('=')
                .Append(stage.DurationMs.ToString("F1", CultureInfo.InvariantCulture))
                .Append("ms");

            if (!string.IsNullOrEmpty(stage.Detail))
            {
                builder.Append(" (")
                    .Append(stage.Detail)
                    .Append(')');
            }
        }

        return builder.ToString();
    }

    private void AddCore(string name, double durationMs, string? detail)
    {
        if (_stages.Count >= MaxStages)
        {
            return;
        }

        _stages.Add(new RequestStage(name, durationMs, detail));
    }

    private IDisposable StartStage(string name) =>
        new StageScope(this, name, Stopwatch.GetTimestamp());

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (var symbol in name)
        {
            builder.Append(char.IsLetterOrDigit(symbol) || symbol is '-' or '_' ? symbol : '-');
        }

        return builder.ToString();
    }

    private sealed class StageScope(
        RequestTimingContext owner,
        string name,
        long startedAt) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.RecordStage(name, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}

/// <summary>Зафиксированный этап обработки запроса.</summary>
/// <param name="Name">Имя этапа.</param>
/// <param name="DurationMs">Длительность в миллисекундах.</param>
/// <param name="Detail">Необязательная деталь (например, текст SQL без параметров).</param>
internal readonly record struct RequestStage(
    string Name,
    double DurationMs,
    string? Detail);
