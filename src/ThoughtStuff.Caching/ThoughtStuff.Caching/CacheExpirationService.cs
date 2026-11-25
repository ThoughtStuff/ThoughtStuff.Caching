#if NETSTANDARD2_0
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#endif

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

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
    public bool IsExpired(DistributedCacheEntryOptions? cacheEntryOptions, DateTimeOffset updatedTime)
    {
        return IsExpired(cacheEntryOptions, updatedTime, DateTimeOffset.Now);
    }

    internal bool IsExpired(DistributedCacheEntryOptions? cacheEntryOptions, DateTimeOffset updatedTime, DateTimeOffset now)
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
        return now >= GetExpiration(cacheEntryOptions, updatedTime);
    }

    /// <summary>Returns a deadline suitable for indexing, or null when trim must evaluate the policy directly.</summary>
    internal DateTimeOffset? GetExpirationHint(DistributedCacheEntryOptions? options, DateTimeOffset updatedTime)
    {
        if (IsNullOrEmpty(options))
        {
            // A custom default policy could supply different options on the next call.
            if (defaultCachePolicyService.GetType() != typeof(HardCodedDefaultCachePolicy))
                return null;
            options = defaultCachePolicyService.GetDefaultCacheEntryOptions();
        }

        return options.SlidingExpiration.HasValue ? null : GetExpiration(options, updatedTime);
    }

    private static DateTimeOffset? GetExpiration(DistributedCacheEntryOptions options, DateTimeOffset updatedTime)
    {
        DateTimeOffset? expiration = options.AbsoluteExpiration?.LocalDateTime;
        if (options.AbsoluteExpirationRelativeToNow is { } interval)
        {
            var relativeExpiration = updatedTime.Add(interval);
            if (expiration is null || relativeExpiration < expiration)
                return relativeExpiration;
        }

        return expiration;
    }

    private static bool IsNullOrEmpty(
#if NETSTANDARD2_1
        [NotNullWhen(false)]
#endif
        DistributedCacheEntryOptions? cacheEntryOptions)
    {
        return cacheEntryOptions is null
               || cacheEntryOptions.AbsoluteExpiration is null
               && cacheEntryOptions.AbsoluteExpirationRelativeToNow is null;
    }
}
