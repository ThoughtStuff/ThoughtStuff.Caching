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

    /// <summary>
    /// Enumerates the keys in the cache.
    /// </summary>
    /// <remarks>
    /// This can be very slow and is not recommended for normal operation.
    /// <para/>
    /// Because cache entries can be added and removed during enumeration there is no guarantee that the enumeration is still accurate when it is completed.
    /// <para/>
    /// Implementations may not track all cache entries that were added outside of a ThoughtStuff.Caching interface.
    /// <para/>
    /// Though <see cref="IAsyncEnumerable{T}"/> is returned, the implementation may in fact be synchronous.
    /// </remarks>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<string> EnumerateKeys(CancellationToken cancellationToken = default);
}
