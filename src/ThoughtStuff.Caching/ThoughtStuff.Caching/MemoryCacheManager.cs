// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace ThoughtStuff.Caching;

internal class MemoryCacheManager : ICacheManager
{
    private readonly IMemoryCache memoryCache;

    public MemoryCacheManager(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    private MemoryCache ConcreteMemoryCache => memoryCache as MemoryCache
                                               ?? throw new NotSupportedException("The current IMemoryCache implementation is not supported by MemoryCacheManager");

    /// <inheritdoc/>
    public Task<int> GetCacheEntryCount()
    {
        return Task.FromResult(ConcreteMemoryCache.Count);
    }

    /// <inheritdoc/>
    public Task<int> DeleteMatchingEntries(string keyWildcardExpression)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task<int> GetCountOfMatchingEntries(string keyWildcardExpression)
    {
        throw new NotSupportedException();
    }
}
