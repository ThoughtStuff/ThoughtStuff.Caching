﻿// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching;

public interface ICacheEntry
{
    DateTimeOffset Updated { get; }
    DistributedCacheEntryOptions Options { get; }
}
