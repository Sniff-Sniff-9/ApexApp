namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Параметры внешних вызовов Supabase: имя зарегистрированного
/// <see cref="System.Net.Http.HttpClient"/> и таймаут запроса.
/// </summary>
public static class SupabaseHttpClient
{
    /// <summary>Имя именованного клиента в <c>IHttpClientFactory</c>.</summary>
    public const string Name = "supabase";

    /// <summary>
    /// Таймаут внешнего запроса. Значение по умолчанию (100 секунд) слишком велико:
    /// при недоступности Supabase запрос API не должен ждать так долго.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Признак недоступности внешнего сервиса: сетевой сбой или таймаут HTTP-клиента.
    /// Отмена запроса самим вызывающим кодом недоступностью не считается.
    /// </summary>
    /// <param name="exception">Исключение внешнего вызова.</param>
    /// <param name="cancellationToken">Токен отмены текущего запроса.</param>
    /// <returns><c>true</c>, если внешний сервис недоступен.</returns>
    public static bool IsUnavailable(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested &&
               (exception is HttpRequestException or TaskCanceledException or TimeoutException);
    }
}
