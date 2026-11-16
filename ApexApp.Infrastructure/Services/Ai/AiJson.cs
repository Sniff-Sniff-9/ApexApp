using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>
/// Разбор JSON-ответов модели. Модель в режиме JSON возвращает объект, но иногда
/// добавляет markdown-обёртку, поэтому содержимое сначала нормализуется.
/// </summary>
public static class AiJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Разбирает ответ модели в указанный тип.
    /// </summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="content">Сырой ответ модели.</param>
    /// <param name="value">Разобранное значение.</param>
    /// <param name="error">Причина неудачи.</param>
    /// <returns><c>true</c>, если ответ разобран.</returns>
    public static bool TryDeserialize<T>(
        string? content,
        out T? value,
        out string? error)
    {
        value = default;
        error = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "The AI response is empty.";
            return false;
        }

        var json = Extract(content);

        if (json.Length == 0)
        {
            error = "The AI response contains no JSON object.";
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }

        if (value is null)
        {
            error = "The AI response contains a null JSON object.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Выделяет JSON-объект из ответа: убирает markdown-обёртку и текст вокруг объекта.
    /// </summary>
    /// <param name="content">Сырой ответ модели.</param>
    /// <returns>Строка JSON или пустая строка, если объект не найден.</returns>
    public static string Extract(string content)
    {
        var text = content.Replace('\u00A0', ' ').Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBreak = text.IndexOf('\n');

            if (firstBreak >= 0)
            {
                text = text[(firstBreak + 1)..];
            }

            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

            if (lastFence >= 0)
            {
                text = text[..lastFence];
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
    }
}
