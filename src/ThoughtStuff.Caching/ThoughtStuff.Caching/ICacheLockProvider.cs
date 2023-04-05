// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ICacheLockProvider
{
    /// <summary>
    /// Returns a <see cref="SemaphoreSlim"/> mutex object that is suitable to use when
    /// invoking an underlying provider in order to update a cache entry.
    /// That is, it returns the same semaphore for the same key, and different semaphores for different keys.
    /// <para/>
    /// Thread safe: Concurrent calls with the same key will get the same semaphore.
    /// </summary>
    SemaphoreSlim GetCacheLockObject(string key);
}
