// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.
using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching;

/// <summary>
/// Must be used as a Singleton.
/// </summary>
public class CacheLockProvider : ICacheLockProvider
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan expiration;
    // Use MemoryCache rather than plain ConcurrentDictionary to support automatic eviction
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly object lockObject = new();

    private CacheLockProvider(TimeSpan expiration)
    {
        this.expiration = expiration;
    }

    public CacheLockProvider() : this(DefaultExpiration) { }

    /// <inheritdoc/>
    public object GetCacheLockObject(string key)
    {
        lock (lockObject)
        {
            return cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = expiration;
                return new object();
            });
        }
    }

    internal static CacheLockProvider WithExpiration(TimeSpan expiration) => new(expiration);
}
