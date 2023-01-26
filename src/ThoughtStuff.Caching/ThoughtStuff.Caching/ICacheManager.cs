// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ICacheManager
{
    /// <summary>
    /// Returns the number of items in the cache.
    /// </summary>
    /// <remarks>
    /// This may be slow O(N) operation for some cache implementations.
    /// </remarks>
    Task<int> GetCacheEntryCount();

    /// <summary>
    /// Returns the number of cache entries with a key matching the <paramref name="keyWildcardExpression"/>.
    /// The <paramref name="keyWildcardExpression"/> may be a simple key string, or may include `*` wildcards.
    /// </summary>
    /// <remarks>
    /// Counting matching keys may be a slow O(N) operation.
    /// </remarks>
    Task<int> GetCountOfMatchingEntries(string keyWildcardExpression);

    /// <summary>
    /// Purges each entry from the cache that has a key matching the <paramref name="keyWildcardExpression"/>.
    /// </summary>
    /// <returns>
    /// The number of entries removed.
    /// </returns>
    Task<int> DeleteMatchingEntries(string keyWildcardExpression);
}
