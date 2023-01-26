// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

public interface ICacheExpirationService
{
    /// <summary>
    /// Returns true if the cache entry is expired
    /// given that cache entry was last updated at <paramref name="updatedTime"/>
    /// and the cache entry has options of <paramref name="cacheEntryOptions"/>.
    /// <para/>
    /// Passing `null` or uninitialized <paramref name="cacheEntryOptions"/>
    /// will cause a default cache policy to be used.
    /// See <seealso cref="IDefaultCachePolicyService"/>.
    /// </summary>
    bool IsExpired(DistributedCacheEntryOptions? cacheEntryOptions, DateTimeOffset updatedTime);
}
