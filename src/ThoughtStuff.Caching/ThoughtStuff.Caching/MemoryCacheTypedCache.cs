// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching;

/// <summary>
/// <see cref="IMemoryCache"/> implementation of <see cref="ITypedCache"/>
/// </summary>
public class MemoryCacheTypedCache : ITypedCache, IManagedCache
{
    private readonly IMemoryCache memoryCache;

    public MemoryCacheTypedCache(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <inheritdoc/>
    public bool Contains(string key) => memoryCache.TryGetValue(key, out _);

    /// <inheritdoc/>
    public string GetLocation(string key) => key;

    /// <inheritdoc/>
    public T Get<T>(string key) => memoryCache.Get<T>(key);

    /// <inheritdoc/>
    public void Set<T>(string key, T value, DistributedCacheEntryOptions options)
    {
        var memCacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = options.AbsoluteExpiration,
            AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = options.SlidingExpiration
        };
        memoryCache.Set(key, value, memCacheOptions);
    }

    /// <inheritdoc/>
    public ICacheManager GetCacheManager()
    {
        return new MemoryCacheManager(memoryCache);
    }
}
