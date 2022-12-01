// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;

namespace ThoughtStuff.Caching;

/// <summary>
/// In-memory Dictionary implementation of ITextCache
/// </summary>
public class DictionaryTextCache : ITextCache, IManagedCache
{
    private readonly Dictionary<string, Entry> dictionary = new();
    private readonly ICacheExpirationService cacheExpirationService;

    public DictionaryTextCache(ICacheExpirationService cacheExpirationService)
    {
        this.cacheExpirationService = cacheExpirationService ?? throw new ArgumentNullException(nameof(cacheExpirationService));
    }

    /// <inheritdoc/>
    public bool Contains(string key)
    {
        var contains = dictionary.ContainsKey(key);
        if (!contains)
            return false;
        var entry = dictionary[key];
        if (cacheExpirationService.IsExpired(entry.Options, entry.Updated))
        {
            dictionary.Remove(key);
            return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public string GetLocation(string key) => key;

    /// <inheritdoc/>
    public string GetString(string key) =>
        Contains(key) ? dictionary[key].Value : default;

    /// <inheritdoc/>
    public void SetString(string key, string value, DistributedCacheEntryOptions options)
    {
        CachingInternal.ProhibitDefaultValue(key, value);
        dictionary[key] = new Entry(value, options);
    }

    /// <inheritdoc/>
    public ICacheManager GetCacheManager()
    {
        return new DictionaryTextCacheManager(dictionary);
    }

    internal class Entry : ITextCacheEntry
    {
        public string Value { get; }
        public DateTimeOffset Updated { get; }
        public DistributedCacheEntryOptions Options { get; }

        public Entry(string content, DistributedCacheEntryOptions options)
        {
            Updated = DateTimeOffset.Now;
            Value = content ?? throw new ArgumentNullException(nameof(content));
            Options = options;
        }
    }
}
