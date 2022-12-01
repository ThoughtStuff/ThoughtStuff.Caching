// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using ThoughtStuff.Core;

namespace ThoughtStuff.Caching;

internal static class CachingInternal
{
    public static void ProhibitDefaultValue<T>(string key, T value)
    {
        // Default (null) values are not allowed in the cache
        if (value.IsDefault())
            throw new ArgumentException($"Default values are not permitted in cache. Key: {key}");
    }
}
