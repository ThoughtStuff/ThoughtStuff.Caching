using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using static ThoughtStuff.Caching.CachingInternal;

namespace ThoughtStuff.Caching;

/// <summary>
/// Wraps a <see cref="ITextCache"/> and uses JSON to serialize objects into and out of the <see cref="ITextCache"/>.
/// </summary>
public class JsonTypedCache(ITextCache textCache) : ITypedCache
{
    private readonly ITextCache textCache = textCache ?? throw new ArgumentNullException(nameof(textCache));

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
    public T? Get<T>(string key) => TryGet<T>(key, out var value) ? value : default;

    internal bool TryGet<T>(string key, out T? value)
    {
        var json = textCache.GetString(key);
        value = json is null ? default : JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        return json is not null;
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T value, DistributedCacheEntryOptions options)
    {
        ProhibitDefaultValue(key, value);
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions);
        textCache.SetString(key, json, options);
    }
}
