using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

public class HardCodedDefaultCachePolicy : IDefaultCachePolicyService
{
    public DistributedCacheEntryOptions GetDefaultCacheEntryOptions()
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };
    }
}
