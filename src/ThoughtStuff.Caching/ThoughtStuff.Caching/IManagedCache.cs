// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface IManagedCache
{
    /// <summary>
    /// Returns a Cache Manager instance for the cache.
    /// </summary>
    ICacheManager GetCacheManager();
}
