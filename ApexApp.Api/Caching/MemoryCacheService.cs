using System.Collections.Concurrent;
using ApexApp.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ApexApp.Api.Caching;

/// <summary>
/// <see cref="ICacheService"/> implementation backed by <see cref="IMemoryCache"/>.
/// The cache lives in the memory of a single API instance.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    // Registry of the keys written by this service; required to drop whole key groups.
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCacheService"/> class.
    /// </summary>
    /// <param name="cache">Memory cache from the dependency injection container.</param>
    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var cached) && cached is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        if (value is null)
        {
            return;
        }

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        options.RegisterPostEvictionCallback(Forget, _keys);

        _cache.Set(key, value, options);
        _keys[key] = 0;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                Remove(key);
            }
        }
    }

    private static void Forget(object key, object? value, EvictionReason reason, object? state)
    {
        // A replaced entry has already been re-registered by the caller.
        if (reason == EvictionReason.Replaced)
        {
            return;
        }

        if (state is ConcurrentDictionary<string, byte> keys && key is string cacheKey)
        {
            keys.TryRemove(cacheKey, out _);
        }
    }
}