using System.Collections.Concurrent;
using ApexApp.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Обёртка над <see cref="ICacheService"/> для аналитических ответов: добавляет защиту
/// от одновременного расчёта одного и того же тяжёлого агрегата (cache stampede).
/// </summary>
public sealed class AnalyticsCache(ICacheService cache, ILogger<AnalyticsCache> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Возвращает значение из кеша или рассчитывает его и сохраняет.
    /// </summary>
    /// <typeparam name="T">Тип значения (ссылочный).</typeparam>
    /// <param name="key">Ключ кеша.</param>
    /// <param name="ttl">Время жизни.</param>
    /// <param name="factory">Фабрика значения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Значение из кеша или рассчитанное значение.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (cache.TryGet<T>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGet<T>(key, out cached) && cached is not null)
            {
                return cached;
            }

            var value = await factory(cancellationToken);

            cache.Set(key, value, ttl);

            logger.LogDebug("Analytics cache miss for {Key}; stored for {Ttl}.", key, ttl);

            return value;
        }
        finally
        {
            gate.Release();

            // Keys carry a time bucket, so the gate registry must not grow forever.
            if (gate.CurrentCount == 1)
            {
                Gates.TryRemove(key, out _);
            }
        }
    }
}
