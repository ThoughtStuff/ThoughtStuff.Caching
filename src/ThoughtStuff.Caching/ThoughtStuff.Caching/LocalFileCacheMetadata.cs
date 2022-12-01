// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

internal class LocalFileCacheMetadata
{
    public DistributedCacheEntryOptions CacheEntryOptions { get; set; }
}
