// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

/// <summary>
/// Based on <see cref="DistributedCacheEntryExtensions" />
/// </summary>
public static class TextCacheExtensions
{
    /// <summary>
    /// Sets cache value with the given key without expiration
    /// </summary>
    public static void SetString(this ITextCache cache, string key, string value) =>
        cache.SetString(key, value, new DistributedCacheEntryOptions());
}
