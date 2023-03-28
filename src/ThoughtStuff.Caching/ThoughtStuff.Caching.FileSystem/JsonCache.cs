// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using static ThoughtStuff.Caching.CachingInternal;

namespace ThoughtStuff.Caching.FileSystem;

/// <summary>
/// Wraps a <see cref="ITextCache"/> and uses JSON to serialize objects into and out of the <see cref="ITextCache"/>.
/// </summary>
public class JsonCache : ITypedCache
{
    private readonly ITextCache textCache;

    public JsonCache(ITextCache textCache)
    {
        this.textCache = textCache ?? throw new ArgumentNullException(nameof(textCache));
    }

    private JsonSerializerSettings JsonSerializerSettings =>
        new JsonSerializerSettings
        {
            // `FilingData` requires preserving references for matching periods
            PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };

    /// <inheritdoc/>
    public bool Contains(string key) => textCache.Contains(key);

    /// <inheritdoc/>
    public string GetLocation(string key) => textCache.GetLocation(key);

    /// <inheritdoc/>
    public void Remove(string key) => textCache.Remove(key);

    /// <inheritdoc/>
    public T? Get<T>(string key)
    {
        var json = textCache.GetString(key);
        if (json is null)
            return default;
        return JsonConvert.DeserializeObject<T>(json, JsonSerializerSettings);
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T value, DistributedCacheEntryOptions options)
    {
        ProhibitDefaultValue(key, value);
        var json = JsonConvert.SerializeObject(value, JsonSerializerSettings);
        textCache.SetString(key, json, options);
    }
}
