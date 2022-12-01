// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;

namespace ThoughtStuff.Caching;

public class CacheExpirationService : ICacheExpirationService
{
    private readonly IDefaultCachePolicyService defaultCachePolicyService;
    private readonly ILogger<CacheExpirationService> logger;

    public CacheExpirationService(IDefaultCachePolicyService defaultCachePolicyService, ILogger<CacheExpirationService> logger)
    {
        this.defaultCachePolicyService = defaultCachePolicyService ?? throw new ArgumentNullException(nameof(defaultCachePolicyService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    ///<inheritdoc/>
    public bool IsExpired(DistributedCacheEntryOptions cacheEntryOptions, DateTimeOffset updatedTime)
    {
        return IsExpired(cacheEntryOptions, updatedTime, DateTimeOffset.Now);
    }

    internal bool IsExpired(DistributedCacheEntryOptions cacheEntryOptions, DateTimeOffset updatedTime, DateTimeOffset now)
    {
        if (updatedTime > now)
            throw new ArgumentException("Last Update must have occurred in the past", nameof(now));
        if (IsNullOrEmpty(cacheEntryOptions))
        {
            logger.LogTrace("Falling back to GetDefaultCacheEntryOptions because cacheEntryOptions not initialized for item last updated at {updatedTime}.", updatedTime);
            cacheEntryOptions = defaultCachePolicyService.GetDefaultCacheEntryOptions();
        }
        if (cacheEntryOptions.SlidingExpiration.HasValue)
            throw new NotImplementedException("Sliding Expiration cache policy is not implemented");
        // Initialize expiration to "tomorrow" so selection of relative expiration simpler below
        var expiration = now.AddDays(1);
        var absolute = cacheEntryOptions.AbsoluteExpiration;
        if (absolute.HasValue)
            expiration = absolute.Value.LocalDateTime;
        var relativeTimespan = cacheEntryOptions.AbsoluteExpirationRelativeToNow;
        if (relativeTimespan.HasValue)
        {
            var relativeExpiration = updatedTime.Add(relativeTimespan.Value);
            // Choose earlier expiration (relative vs absolute) if both provided
            if (relativeExpiration < expiration)
                expiration = relativeExpiration;
        }
        return now >= expiration;
    }

    private static bool IsNullOrEmpty(DistributedCacheEntryOptions cacheEntryOptions)
    {
        return cacheEntryOptions is null ||
            cacheEntryOptions.AbsoluteExpiration is null && cacheEntryOptions.AbsoluteExpirationRelativeToNow is null;
    }
}
