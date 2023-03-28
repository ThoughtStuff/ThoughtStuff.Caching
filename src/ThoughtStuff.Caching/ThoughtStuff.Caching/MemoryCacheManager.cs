// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using ThoughtStuff.Caching.Core;

namespace ThoughtStuff.Caching;

internal class MemoryCacheManager : ICacheManager
{
    private readonly IMemoryCache memoryCache;

    public MemoryCacheManager(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    private MemoryCache ConcreteMemoryCache => memoryCache as MemoryCache
                                               ?? throw new NotSupportedException("The current IMemoryCache implementation is not supported by MemoryCacheManager");

    private IEnumerable<object> EntriesCollection
    {
        get
        {
            // EVIL: Using reflection to get access to the private concurrent dictionary that MemoryCache uses
            return ConcreteMemoryCache.GetPropertyValue<ICollection>("EntriesCollection")
                                      .Cast<object>();
            // TODO: Cast to IEnumerable<KeyValuePair<object, ICacheEntry>>,
            // but concrete type is KeyValuePair<object, internal CacheEntry>
            // and KeyValuePair doesn't support variance
        }
    }

    /// <inheritdoc/>
    public Task<int> GetCacheEntryCount()
    {
        return Task.FromResult(ConcreteMemoryCache.Count);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteMatchingEntries(string keyWildcardExpression)
    {
        var matchingKeys = GetMatchingKeys(keyWildcardExpression);
        int count = 0;
        await foreach (var key in matchingKeys)
        {
            memoryCache.Remove(key);
            ++count;
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> GetCountOfMatchingEntries(string keyWildcardExpression)
    {
        var matchingKeys = GetMatchingKeys(keyWildcardExpression);
        return await matchingKeys.CountAsync();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> EnumerateKeys(CancellationToken cancellationToken = default)
    {
        // Each item is a KeyValuePair<object, ICacheEntry>
        return EntriesCollection.Select(item => item.GetPropertyValue<string>("Key"))
                                .ToAsyncEnumerable();
    }

    private IAsyncEnumerable<string> GetMatchingKeys(string keyWildcardExpression)
    {
        var regex = StringUtilities.WildcardToRegex(keyWildcardExpression);
        var matchingKeys = EnumerateKeys().Where(key => regex.IsMatch(key));
        return matchingKeys;
    }
}
