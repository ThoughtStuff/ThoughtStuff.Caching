using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json.Serialization;

namespace ThoughtStuff.Caching.FileSystem;

/// <summary>Expiration options and an optional entry version stored beside the payload.</summary>
internal class LocalFileCacheMetadata
{
    public DistributedCacheEntryOptions? CacheEntryOptions { get; set; }

    /// <summary>
    /// A fresh token generated for each capacity-managed write to this cache key, even when
    /// the payload is unchanged. The catalog combines this token with file stamps so eviction
    /// and buffered hits can distinguish a replacement from the version they originally saw.
    /// </summary>
    /// <remarks>Older entries omit this token. Inventory derives their version from file stamps.</remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntryVersion { get; set; }
}
