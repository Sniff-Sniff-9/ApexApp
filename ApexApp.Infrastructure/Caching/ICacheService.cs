namespace ApexApp.Infrastructure.Caching;

/// <summary>
/// Application cache abstraction. The implementation is registered in the API layer
/// and is backed by <c>IMemoryCache</c>.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Tries to read a cached value.
    /// </summary>
    /// <typeparam name="T">Cached value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Cached value when found.</param>
    /// <returns><c>true</c> when the value is present in the cache.</returns>
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// Stores a value in the cache for the given time to live.
    /// </summary>
    /// <typeparam name="T">Cached value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache. Null values are ignored.</param>
    /// <param name="ttl">Absolute time to live.</param>
    void Set<T>(string key, T value, TimeSpan ttl);

    /// <summary>
    /// Removes a single cache entry.
    /// </summary>
    /// <param name="key">Cache key.</param>
    void Remove(string key);

    /// <summary>
    /// Removes all cache entries whose key starts with the given prefix.
    /// </summary>
    /// <param name="prefix">Key prefix.</param>
    void RemoveByPrefix(string prefix);
}