using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Api.DTOs.Ai;

/// <summary>
/// Служебные метрики генерации: модель, время, расход токенов.
/// </summary>
/// <param name="Model">Использованная модель.</param>
/// <param name="GeneratedAt">Момент генерации (UTC).</param>
/// <param name="PromptTokens">Токены запроса.</param>
/// <param name="CompletionTokens">Токены ответа.</param>
/// <param name="Truncated">Признак обрыва ответа по лимиту токенов.</param>
public sealed record AiMetaDto(
    string Model,
    DateTime GeneratedAt,
    long PromptTokens,
    long CompletionTokens,
    bool Truncated)
{
    /// <summary>Создаёт DTO из метрик сервиса.</summary>
    /// <param name="meta">Метрики генерации.</param>
    /// <returns>DTO метрик.</returns>
    public static AiMetaDto From(AiGenerationMeta meta) =>
        new(
            meta.Model,
            meta.GeneratedAt,
            meta.PromptTokens,
            meta.CompletionTokens,
            meta.Truncated);
}

/// <summary>
/// Ответ модуля ИИ: содержимое и метрики генерации.
/// </summary>
/// <typeparam name="T">Тип содержимого.</typeparam>
/// <param name="Content">Содержимое.</param>
/// <param name="Meta">Метрики генерации.</param>
public sealed record AiResultDto<T>(T Content, AiMetaDto Meta)
{
    /// <summary>Создаёт ответ из результата сервиса.</summary>
    /// <param name="content">Содержимое.</param>
    /// <param name="meta">Метрики генерации.</param>
    /// <returns>DTO ответа.</returns>
    public static AiResultDto<T> Create(T content, AiGenerationMeta meta) =>
        new(content, AiMetaDto.From(meta));
}
