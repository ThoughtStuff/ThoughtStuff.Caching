// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ICacheBase
{
    /// <summary>
    /// Returns true if an entry is in the cache and not expired.
    /// If an entry is expired it will be evicted.
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

    /// <summary>
    /// Removes the entry associated with the given <paramref name="key"/>.
    /// </summary>
    void Remove(string key);
}
