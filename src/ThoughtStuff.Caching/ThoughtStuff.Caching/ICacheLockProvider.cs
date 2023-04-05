// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ICacheLockProvider
{
    /// <summary>
    /// Returns a lock object that is suitable to use when updating a cache entry.
    /// That is, it returns the same object for the same key, and different objects for different keys.
    /// <para/>
    /// Thread safe: Concurrent calls with the same key will get the same lock object.
    /// </summary>
    object GetCacheLockObject(string key);
}
