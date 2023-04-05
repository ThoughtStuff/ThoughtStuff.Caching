// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.
using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching;

/// <summary>
/// Must be used as a Singleton because it holds a collection of locks.
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
    public SemaphoreSlim GetCacheLockObject(string key)
    {
        lock (lockObject)
        {
            return cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = expiration;
                return new SemaphoreSlim(1);
            });
        }
    }

    internal static CacheLockProvider WithExpiration(TimeSpan expiration) => new(expiration);
}
