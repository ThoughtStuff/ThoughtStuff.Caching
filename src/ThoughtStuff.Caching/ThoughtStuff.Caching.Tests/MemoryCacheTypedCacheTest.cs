// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching.Tests;

public class MemoryCacheTypedCacheTest
{
    [Theory(DisplayName = "MemoryCache: Location Matches Key"), AutoMoq]
    public void Location(MemoryCacheTypedCache cache, string key)
    {
        cache.GetLocation(key).Should().Be(key);
    }

    [Fact(DisplayName = "MemoryCache: Basic usage")]
    public void MemoryCacheBasic()
    {
        var memCacheOptions = new MemoryCacheOptions();
        var memCache = new MemoryCache(memCacheOptions);
        var subject = new MemoryCacheTypedCache(memCache);
        const string key = "test-key";
        const string value = "value 123";
        subject.Contains(key).Should().BeFalse();
        subject.Get<string>(key).Should().BeNull();

        subject.Set(key, value);

        subject.Contains(key).Should().BeTrue();
        subject.Get<string>(key).Should().Be(value);
    }
}
