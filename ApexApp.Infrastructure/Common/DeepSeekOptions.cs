namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Настройки интеграции с DeepSeek. Секция конфигурации: <c>DeepSeek</c>.
/// </summary>
/// <remarks>
/// Ключ API не хранится в репозитории. Он задаётся через User Secrets
/// (<c>dotnet user-secrets set "DeepSeek:ApiKey" "..."</c>) или через переменную
/// окружения <c>DeepSeek__ApiKey</c> (дополнительно поддерживается <c>DEEPSEEK_API_KEY</c>).
/// Значения не хранятся в БД, поэтому их изменение не требует миграций.
/// </remarks>
public sealed class DeepSeekOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "DeepSeek";

    /// <summary>Базовый адрес официального API DeepSeek.</summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>Идентификатор модели: <c>deepseek-flash</c> или <c>deepseek-v4-pro</c>.</summary>
    public string Model { get; set; } = "deepseek-flash";

    /// <summary>Ключ API. Задаётся только через секреты или переменные окружения.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Таймаут одного запроса к провайдеру в секундах.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Ограничение на количество токенов в ответе модели.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Температура для генерации текстов.</summary>
    public double ContentTemperature { get; set; } = 0.7d;

    /// <summary>Температура для аналитических рекомендаций.</summary>
    public double AnalysisTemperature { get; set; } = 0.3d;

    /// <summary>Максимальная длина сводки статистики, передаваемой модели.</summary>
    public int MaxPromptChars { get; set; } = 12_000;

    /// <summary>Количество повторных попыток при временных ошибках провайдера (0–2).</summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>Признак заполненной конфигурации. Без ключа модуль отвечает 503, приложение работает.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model);
}
