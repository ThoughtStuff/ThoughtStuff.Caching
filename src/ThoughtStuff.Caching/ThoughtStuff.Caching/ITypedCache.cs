// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

/// <summary>
/// Cache for storing strongly typed values.
/// </summary>
public interface ITypedCache : ICacheBase
{
    /// <summary>
    /// Return the cached value if present and not expired.
    /// Returns <see cref="default"/>(<typeparamref name="T"/>) otherwise.
    /// </summary>
    T Get<T>(string key);

    /// <summary>
    /// Store the value in the cache using the given <see cref="DistributedCacheEntryOptions"/>.
    /// Setting a value of <see cref="default"/>(<typeparamref name="T"/>) is not permitted.
    /// <para/>
    /// For in-memory caches the value may be held by reference.
    /// </summary>
    void Set<T>(string key, T value, DistributedCacheEntryOptions options);
}
