using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

public interface IDefaultCachePolicyService
{
    DistributedCacheEntryOptions GetDefaultCacheEntryOptions();
}
