// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.Json.Serialization;
using static ThoughtStuff.Caching.CachingInternal;

namespace ThoughtStuff.Caching;

/// <summary>
/// Wraps a <see cref="ITextCache"/> and uses JSON to serialize objects into and out of the <see cref="ITextCache"/>.
/// </summary>
public class JsonTypedCache : ITypedCache
{
    private readonly ITextCache textCache;

    public JsonTypedCache(ITextCache textCache)
    {
        this.textCache = textCache ?? throw new ArgumentNullException(nameof(textCache));
    }

    private JsonSerializerOptions JsonSerializerOptions => new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
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
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T value, DistributedCacheEntryOptions options)
    {
        ProhibitDefaultValue(key, value);
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions);
        textCache.SetString(key, json, options);
    }
}
