namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Запрос к провайдеру ИИ. Провайдер получает только подготовленные Backend-ом
/// сообщения: системную инструкцию и пользовательский текст.
/// </summary>
/// <param name="SystemPrompt">Системная инструкция.</param>
/// <param name="UserPrompt">Пользовательский текст с данными.</param>
/// <param name="MaxTokens">Ограничение на количество токенов ответа.</param>
/// <param name="Temperature">Температура генерации.</param>
/// <param name="JsonMode">Признак требования валидного JSON в ответе.</param>
public sealed record AiCompletionRequest(
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens,
    double Temperature,
    bool JsonMode = true);

/// <summary>
/// Ответ провайдера ИИ вместе со служебными метриками.
/// </summary>
/// <param name="Content">Текст ответа.</param>
/// <param name="Model">Фактически использованная модель.</param>
/// <param name="PromptTokens">Токены запроса.</param>
/// <param name="CompletionTokens">Токены ответа.</param>
/// <param name="Truncated">Признак обрыва ответа по лимиту токенов.</param>
public sealed record AiCompletion(
    string Content,
    string Model,
    long PromptTokens,
    long CompletionTokens,
    bool Truncated);

/// <summary>
/// Транспортный клиент провайдера ИИ. Не знает о предметной области и не имеет
/// доступа к базе данных.
/// </summary>
public interface IAiClient
{
    /// <summary>Признак заполненной конфигурации.</summary>
    bool IsConfigured { get; }

    /// <summary>Настроенная модель.</summary>
    string Model { get; }

    /// <summary>
    /// Отправляет запрос провайдеру. Ошибки транспорта выражаются типизированными
    /// исключениями, которые сервисы преобразуют в <c>ServiceResult</c>.
    /// </summary>
    /// <param name="request">Подготовленный запрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ провайдера.</returns>
    /// <exception cref="AiNotConfiguredException">Ключ API не задан.</exception>
    /// <exception cref="AiTimeoutException">Провайдер не ответил за отведённое время.</exception>
    /// <exception cref="AiProviderException">Провайдер вернул ошибку или непригодный ответ.</exception>
    Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Ключ API не задан, поэтому обращения к провайдеру невозможны.</summary>
public sealed class AiNotConfiguredException : Exception
{
    /// <summary>Создаёт исключение.</summary>
    public AiNotConfiguredException()
        : base("The DeepSeek API key is not configured.")
    {
    }
}

/// <summary>Провайдер не ответил за отведённое время.</summary>
public sealed class AiTimeoutException : Exception
{
    /// <summary>Создаёт исключение.</summary>
    /// <param name="timeout">Истёкший таймаут.</param>
    public AiTimeoutException(TimeSpan timeout)
        : base($"The AI provider did not respond within {timeout.TotalSeconds:0} seconds.")
    {
        Timeout = timeout;
    }

    /// <summary>Истёкший таймаут.</summary>
    public TimeSpan Timeout { get; }
}

/// <summary>Провайдер вернул ошибку или непригодный ответ.</summary>
public sealed class AiProviderException : Exception
{
    /// <summary>Создаёт исключение.</summary>
    /// <param name="message">Диагностическое сообщение без чувствительных данных.</param>
    /// <param name="statusCode">HTTP-код ответа провайдера или 502 для непригодного ответа.</param>
    /// <param name="innerException">Внутреннее исключение, если есть.</param>
    public AiProviderException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>HTTP-код ответа провайдера или 502 для непригодного ответа.</summary>
    public int StatusCode { get; }
}

/// <summary>Сообщения об ошибках модуля ИИ, возвращаемые клиенту API.</summary>
public static class AiMessages
{
    /// <summary>Ключ API не задан.</summary>
    public const string NotConfigured =
        "AI is not configured. Set DeepSeek:ApiKey in user secrets or the DeepSeek__ApiKey environment variable.";

    /// <summary>Провайдер не ответил вовремя.</summary>
    public const string Timeout = "The AI provider did not respond in time. Try again later.";

    /// <summary>Провайдер вернул ошибку.</summary>
    public const string ProviderFailure = "The AI provider returned an error. Try again later.";

    /// <summary>Ответ не удалось разобрать.</summary>
    public const string InvalidResponse =
        "The AI returned a response that could not be used. Try again or refine the request.";

    /// <summary>Ответ не содержит пригодного содержимого.</summary>
    public const string EmptyResponse =
        "The AI returned no usable content. Try again or refine the request.";
}
