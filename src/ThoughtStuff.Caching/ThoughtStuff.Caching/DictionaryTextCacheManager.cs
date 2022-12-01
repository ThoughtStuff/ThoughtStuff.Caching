// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ThoughtStuff.Core.StringUtilities;

namespace ThoughtStuff.Caching;

public class DictionaryTextCacheManager : ICacheManager
{
    private readonly Dictionary<string, DictionaryTextCache.Entry> dictionary;

    internal DictionaryTextCacheManager(Dictionary<string, DictionaryTextCache.Entry> dictionary)
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
            dictionary.Remove(key);
            ++count;
        }
        return Task.FromResult(count);
    }

    private IEnumerable<string> GetMatchingKeys(string keyWildcardExpression)
    {
        var regex = WildcardToRegex(keyWildcardExpression);
        var matchingKeys = dictionary
            .Keys
            .Where(key => regex.IsMatch(key));
        return matchingKeys;
    }
}
