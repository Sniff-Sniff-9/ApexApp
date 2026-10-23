namespace ApexApp.Api.Diagnostics;

/// <summary>
/// Настройки локальной диагностики производительности.
/// Секция конфигурации: <c>Diagnostics:Timing</c>. По умолчанию диагностика выключена.
/// </summary>
internal sealed class DiagnosticsOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Diagnostics:Timing";

    /// <summary>Включает сбор таймингов. Работает только в Development.</summary>
    public bool Enabled { get; set; }

    /// <summary>Добавляет в лог текст SQL-команды (без параметров).</summary>
    public bool IncludeSql { get; set; }

    /// <summary>Писать строку лога для каждого запроса. Если <c>false</c>, пишутся только медленные.</summary>
    public bool LogAllRequests { get; set; } = true;

    /// <summary>Порог «медленного» запроса в миллисекундах (используется при <c>LogAllRequests=false</c>).</summary>
    public int SlowRequestMs { get; set; }
}
