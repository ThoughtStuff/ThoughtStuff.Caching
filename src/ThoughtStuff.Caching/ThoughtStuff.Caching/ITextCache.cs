// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching
{
    /// <summary>
    /// Cache for storing plain text values.
    /// </summary>
    public interface ITextCache : ICacheBase
    {
        /// <summary>
        /// Return the cached string value if present and not expired.
        /// Returns null otherwise.
        /// </summary>
        string GetString(string key);

        /// <summary>
        /// Set the string value in the cache using the given <see cref="DistributedCacheEntryOptions"/>
        /// </summary>
        void SetString(string key, string value, DistributedCacheEntryOptions options);
    }
}
