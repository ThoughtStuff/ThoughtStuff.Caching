// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.
using System.Collections.Concurrent;

namespace ThoughtStuff.Caching;

public class CacheLockProvider : ICacheLockProvider
{
    private readonly ConcurrentDictionary<string, object> locks = new();

    /// <inheritdoc/>
    public object GetCacheLockObject(string key)
    {
        return locks.GetOrAdd(key, new object());
    }
}
