// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ICacheBase
{
    /// <summary>
    /// Returns true if an item is in the cache and not expired.
    /// If an item is expired it will be evicted.
    /// </summary>
    bool Contains(string key);

    /// <summary>
    /// Returns an implementation-specific locator for the cache entry.
    /// For example a file system cache could return the file path.
    /// A mem cache could return the key or address.
    /// A cloud cache could return the URI.
    /// </summary>
    /// <remarks>
    /// The location may not represent a key that is actually present (contained) in the cache.
    /// The location may be hypothetical.
    /// </remarks>
    string GetLocation(string key);
}
