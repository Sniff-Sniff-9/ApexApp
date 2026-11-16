using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Транспортный клиент официального API DeepSeek: режим JSON, отключённое
/// «размышление», таймаут на запрос, ограниченное число повторов и журналирование.
/// Клиент не знает о предметной области и не обращается к базе данных.
/// </summary>
public sealed class DeepSeekAiClient : IAiClient
{
    private const int MaxAttemptsLimit = 3;

    /// <summary>
    /// Ограничение одновременных обращений к провайдеру на процесс: защищает ключ
    /// от всплеска административных запросов.
    /// </summary>
    private static readonly SemaphoreSlim ConcurrencyGate = new(4, 4);

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekAiClient> _logger;

    /// <summary>Создаёт клиент.</summary>
    /// <param name="httpClient">Настроенный HTTP-клиент с базовым адресом.</param>
    /// <param name="options">Настройки интеграции.</param>
    /// <param name="logger">Журнал.</param>
    public DeepSeekAiClient(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConfigured => _options.IsConfigured;

    /// <inheritdoc />
    public string Model => _options.Model;

    /// <inheritdoc />
    public async Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));
        var maxAttempts = 1 + Math.Clamp(_options.RetryCount, 0, MaxAttemptsLimit - 1);

        await ConcurrencyGate.WaitAsync(cancellationToken);

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var response = await SendAsync(request, timeoutSource.Token);
                    var body = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                    var status = (int)response.StatusCode;

                    if ((status == 429 || status >= 500) && attempt < maxAttempts)
                    {
                        var delay = RetryDelay(response);

                        _logger.LogWarning(
                            "DeepSeek returned {StatusCode}; retrying in {DelayMs} ms (attempt {Attempt}/{MaxAttempts}).",
                            status,
                            (long)delay.TotalMilliseconds,
                            attempt,
                            maxAttempts);

                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("DeepSeek returned {StatusCode}.", status);

                        throw new AiProviderException($"DeepSeek returned HTTP {status}.", status);
                    }

                    var completion = ReadCompletion(body);

                    if (completion is not null)
                    {
                        return completion;
                    }

                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "DeepSeek returned an empty completion; retrying (attempt {Attempt}/{MaxAttempts}).",
                            attempt,
                            maxAttempts);

                        continue;
                    }

                    throw new AiProviderException("DeepSeek returned an empty completion.", 502);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AiTimeoutException(timeout);
                }
                catch (HttpRequestException exception)
                {
                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            exception,
                            "DeepSeek request failed; retrying (attempt {Attempt}/{MaxAttempts}).",
                            attempt,
                            maxAttempts);

                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                        continue;
                    }

                    throw new AiProviderException("DeepSeek request failed.", 502, exception);
                }
            }

            throw new AiProviderException("DeepSeek request failed.", 502);
        }
        finally
        {
            ConcurrencyGate.Release();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt }
            },
            ["stream"] = false,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = request.Temperature,
            ["thinking"] = new JsonObject { ["type"] = "disabled" }
        };

        if (request.JsonMode)
        {
            payload["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private AiCompletion? ReadCompletion(string body)
    {
        DeepSeekChatResponse? payload;

        try
        {
            payload = JsonSerializer.Deserialize<DeepSeekChatResponse>(body, ResponseOptions);
        }
        catch (JsonException exception)
        {
            throw new AiProviderException("DeepSeek returned malformed JSON.", 502, exception);
        }

        var choice = payload?.Choices?.FirstOrDefault();
        var content = choice?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return new AiCompletion(
            content,
            string.IsNullOrWhiteSpace(payload?.Model) ? _options.Model : payload!.Model!,
            payload?.Usage?.PromptTokens ?? 0,
            payload?.Usage?.CompletionTokens ?? 0,
            string.Equals(choice?.FinishReason, "length", StringComparison.OrdinalIgnoreCase));
    }

    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is TimeSpan delta
            && delta > TimeSpan.Zero
            && delta <= TimeSpan.FromSeconds(5))
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var wait = date - DateTimeOffset.UtcNow;

            if (wait > TimeSpan.Zero && wait <= TimeSpan.FromSeconds(5))
            {
                return wait;
            }
        }

        return TimeSpan.FromMilliseconds(500);
    }

    /// <summary>Ответ Chat Completions, ограниченный используемыми полями.</summary>
    internal sealed class DeepSeekChatResponse
    {
        public string? Model { get; set; }

        public List<DeepSeekChoice>? Choices { get; set; }

        public DeepSeekUsage? Usage { get; set; }
    }

    /// <summary>Один вариант ответа модели.</summary>
    internal sealed class DeepSeekChoice
    {
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        public DeepSeekResponseMessage? Message { get; set; }
    }

    /// <summary>Сообщение модели.</summary>
    internal sealed class DeepSeekResponseMessage
    {
        public string? Content { get; set; }
    }

    /// <summary>Расход токенов.</summary>
    internal sealed class DeepSeekUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public long PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public long CompletionTokens { get; set; }
    }
}
