// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.Xunit2;
using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching.Tests;

public class ExpirationTests
{
    [Theory(DisplayName = "Caching: Expiration Options")]
    [InlineAutoMoq("2020-11-02", null, true)]
    [InlineAutoMoq("2020-11-14", null, true)]
    [InlineAutoMoq("2020-11-15", null, true)]
    [InlineAutoMoq("2020-11-16", null, false)]
    [InlineAutoMoq("2020-11-17", null, false)]
    [InlineAutoMoq("2020-11-30", null, false)]
    [InlineAutoMoq(null, 1, true)]
    [InlineAutoMoq(null, 13, true)]
    [InlineAutoMoq(null, 14, true)]
    [InlineAutoMoq(null, 15, false)]
    [InlineAutoMoq(null, 16, false)]
    [InlineAutoMoq(null, 30, false)]
    [InlineAutoMoq("2020-11-14", 1, true)]
    [InlineAutoMoq("2020-11-14", 30, true)]
    [InlineAutoMoq("2020-11-30", 14, true)]
    [InlineAutoMoq("2020-11-30", 30, false)]
    public void ExpirationOptions(string absolute,
                                  int? relativeDays,
                                  bool expected,
                                  CacheExpirationService cacheExpirationService)
    {
        var updated = new DateTime(2020, 11, 01);
        var now = new DateTime(2020, 11, 15);
        var options = new DistributedCacheEntryOptions();
        if (absolute is not null)
            options.AbsoluteExpiration = DateTimeOffset.Parse(absolute);
        if (relativeDays.HasValue)
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(relativeDays.Value);
        cacheExpirationService.IsExpired(options, updated, now)
            .Should().Be(expected);
    }

    [Theory(DisplayName = "Caching: Use Default Expiration Policy")]
    [InlineAutoMoq("2020-11-02", null, true)]
    [InlineAutoMoq("2020-11-14", null, true)]
    [InlineAutoMoq("2020-11-16", null, false)]
    [InlineAutoMoq("2020-11-17", null, false)]
    [InlineAutoMoq(null, 13, true)]
    [InlineAutoMoq(null, 14, true)]
    [InlineAutoMoq(null, 15, false)]
    [InlineAutoMoq(null, 16, false)]
    public void DefaultExpiration(string absolute,
                                  int? relativeDays,
                                  bool expected,
                                  [Frozen] Mock<IDefaultCachePolicyService> defaultCachePolicy,
                                  CacheExpirationService cacheExpirationService)
    {
        var updated = new DateTime(2020, 11, 01);
        var now = new DateTime(2020, 11, 15);
        var options = new DistributedCacheEntryOptions();
        if (absolute is not null)
            options.AbsoluteExpiration = DateTimeOffset.Parse(absolute);
        if (relativeDays.HasValue)
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(relativeDays.Value);

        defaultCachePolicy.Setup(d => d.GetDefaultCacheEntryOptions())
            .Returns(options);

        // Both null and uninitialized DistributedCacheEntryOptions 
        // should be replaced by the default cache policy
        cacheExpirationService.IsExpired(null, updated, now)
            .Should().Be(expected, "failed passing null cache options object");
        cacheExpirationService.IsExpired(new DistributedCacheEntryOptions(), updated, now)
            .Should().Be(expected, "failed passing an empty cache options object");
    }
}
