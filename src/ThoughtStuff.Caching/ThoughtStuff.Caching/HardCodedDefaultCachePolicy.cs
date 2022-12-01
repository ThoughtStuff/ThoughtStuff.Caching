// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using System;

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
