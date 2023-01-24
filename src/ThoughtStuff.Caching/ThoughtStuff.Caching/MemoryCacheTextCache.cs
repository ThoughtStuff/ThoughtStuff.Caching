// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace ThoughtStuff.Caching;

/// <summary>
/// <see cref="IMemoryCache"/> implementation of <see cref="ITextCache"/>
/// </summary>
public class MemoryCacheTextCache : ITextCache
{
    private readonly IMemoryCache memoryCache;

    public MemoryCacheTextCache(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <inheritdoc/>
    public bool Contains(string key) => memoryCache.TryGetValue(key, out _);

    /// <inheritdoc/>
    public string GetLocation(string key) => key;

    /// <inheritdoc/>
    public string GetString(string key) => memoryCache.Get<string>(key);

    /// <inheritdoc/>
    public void SetString(string key, string value, DistributedCacheEntryOptions options)
    {
        var memCacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = options.AbsoluteExpiration,
            AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
            SlidingExpiration = options.SlidingExpiration
        };
        memoryCache.Set(key, value, memCacheOptions);
    }
}
