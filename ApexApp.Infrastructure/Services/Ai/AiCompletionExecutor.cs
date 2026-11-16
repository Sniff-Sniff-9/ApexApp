using System.Diagnostics;
using ApexApp.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Единая точка обращения к провайдеру для прикладных сервисов: проверяет
/// конфигурацию, ограничивает параметры, журналирует метрики и преобразует
/// транспортные ошибки в <see cref="ServiceResult{T}"/>.
/// </summary>
public sealed class AiCompletionExecutor(
    IAiClient aiClient,
    IOptions<DeepSeekOptions> options,
    ILogger<AiCompletionExecutor> logger)
{
    private const int MinTokens = 256;
    private const int MaxTokens = 4000;

    private readonly DeepSeekOptions _options = options.Value;

    /// <summary>Признак заполненной конфигурации провайдера.</summary>
    public bool IsConfigured => aiClient.IsConfigured;

    /// <summary>Настроенная модель.</summary>
    public string Model => aiClient.Model;

    /// <summary>
    /// Выполняет запрос к провайдеру и приводит ошибки к кодам ответа API.
    /// </summary>
    /// <param name="kind">Машинный код сценария для журнала.</param>
    /// <param name="systemPrompt">Системная инструкция.</param>
    /// <param name="userPrompt">Пользовательский промпт.</param>
    /// <param name="temperature">Температура генерации.</param>
    /// <param name="adminId">Администратор, инициировавший запрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Ответ провайдера или ошибка 502/503/504.</returns>
    public async Task<ServiceResult<AiCompletion>> ExecuteAsync(
        string kind,
        string systemPrompt,
        string userPrompt,
        double temperature,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        if (!aiClient.IsConfigured)
        {
            return ServiceResult<AiCompletion>.Fail(503, AiMessages.NotConfigured);
        }

        var maxTokens = Math.Clamp(_options.MaxTokens, MinTokens, MaxTokens);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var completion = await aiClient.CompleteAsync(
                new AiCompletionRequest(systemPrompt, userPrompt, maxTokens, temperature),
                cancellationToken);

            logger.LogInformation(
                "AI request completed: kind={Kind}, admin={AdminId}, elapsedMs={ElapsedMs}, " +
                "model={Model}, promptTokens={PromptTokens}, completionTokens={CompletionTokens}, " +
                "truncated={Truncated}.",
                kind,
                adminId,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                completion.Model,
                completion.PromptTokens,
                completion.CompletionTokens,
                completion.Truncated);

            return ServiceResult<AiCompletion>.Ok(completion);
        }
        catch (AiNotConfiguredException)
        {
            return ServiceResult<AiCompletion>.Fail(503, AiMessages.NotConfigured);
        }
        catch (AiTimeoutException exception)
        {
            logger.LogWarning(
                "AI request timed out: kind={Kind}, admin={AdminId}, timeoutSeconds={TimeoutSeconds}.",
                kind,
                adminId,
                exception.Timeout.TotalSeconds);

            return ServiceResult<AiCompletion>.Fail(504, AiMessages.Timeout);
        }
        catch (AiProviderException exception)
        {
            logger.LogWarning(
                exception,
                "AI request failed: kind={Kind}, admin={AdminId}, providerStatus={ProviderStatus}.",
                kind,
                adminId,
                exception.StatusCode);

            return ServiceResult<AiCompletion>.Fail(502, AiMessages.ProviderFailure);
        }
    }
}
