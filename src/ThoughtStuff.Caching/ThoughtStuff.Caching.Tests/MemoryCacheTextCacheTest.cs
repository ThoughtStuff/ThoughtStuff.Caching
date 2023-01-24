// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching.Tests;

public class MemoryCacheTextCacheTest
{
    [Theory(DisplayName = "MemoryCache: Location Matches Key"), AutoMoq]
    public void Location(MemoryCacheTextCache cache, string key)
    {
        cache.GetLocation(key).Should().Be(key);
    }

    [Fact(DisplayName = "MemoryCache: Basic usage")]
    public void SetupMemoryCache()
    {
        var memCacheOptions = new MemoryCacheOptions();
        var memCache = new MemoryCache(memCacheOptions);
        var subject = new MemoryCacheTextCache(memCache);
        const string key = "test-key";
        const string value = "value 123";
        subject.Contains(key).Should().BeFalse();
        subject.GetString(key).Should().BeNull();

        subject.SetString(key, value);

        subject.Contains(key).Should().BeTrue();
        subject.GetString(key).Should().Be(value);
    }
}
