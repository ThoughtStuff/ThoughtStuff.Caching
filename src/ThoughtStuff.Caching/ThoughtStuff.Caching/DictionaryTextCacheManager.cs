// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Collections.Concurrent;
using static ThoughtStuff.Caching.Core.StringUtilities;

namespace ThoughtStuff.Caching;

public class DictionaryTextCacheManager : ICacheManager
{
    private readonly ConcurrentDictionary<string, DictionaryTextCache.Entry> dictionary;

    internal DictionaryTextCacheManager(ConcurrentDictionary<string, DictionaryTextCache.Entry> dictionary)
    {
        this.dictionary = dictionary;
    }

    /// <inheritdoc/>
    public Task<int> GetCacheEntryCount()
    {
        return Task.FromResult(dictionary.Count);
    }

    /// <inheritdoc/>
    public Task<int> GetCountOfMatchingEntries(string keyWildcardExpression)
    {
        var matchingKeys = GetMatchingKeys(keyWildcardExpression);
        var count = matchingKeys.Count();
        return Task.FromResult(count);
    }

    /// <inheritdoc/>
    public Task<int> DeleteMatchingEntries(string keyWildcardExpression)
    {
        var matchingKeys = GetMatchingKeys(keyWildcardExpression);
        var count = 0;
        foreach (var key in matchingKeys)
        {
            dictionary.TryRemove(key, out _);
            ++count;
        }
        return Task.FromResult(count);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> EnumerateKeys(CancellationToken cancellationToken = default)
    {
        return dictionary.Keys.ToAsyncEnumerable();
    }

    private IEnumerable<string> GetMatchingKeys(string keyWildcardExpression)
    {
        var regex = WildcardToRegex(keyWildcardExpression);
        var matchingKeys = dictionary.Keys
                                     .Where(key => regex.IsMatch(key));
        return matchingKeys;
    }
}
