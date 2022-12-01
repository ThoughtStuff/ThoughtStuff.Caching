// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching
{
    /// <summary>
    /// Based on <see cref="DistributedCacheEntryExtensions" />
    /// </summary>
    public static class TypedCacheExtensions
    {
        /// <summary>
        /// Sets cache value with the given key without expiration
        /// </summary>
        public static void Set<T>(this ITypedCache cache, string key, T item) =>
            cache.Set(key, item, new DistributedCacheEntryOptions());
    }
}
