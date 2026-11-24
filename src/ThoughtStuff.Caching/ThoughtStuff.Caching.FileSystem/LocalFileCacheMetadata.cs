using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching.FileSystem;

internal class LocalFileCacheMetadata
{
    public DistributedCacheEntryOptions? CacheEntryOptions { get; set; }
}
