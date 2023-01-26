// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;

namespace ThoughtStuff.Caching.Tests;

/// <summary>
/// Provides a generic way to test a particular cache <typeparamref name="TCache"/>'s 
/// implementation of the <see cref="ITextCache" /> service contract.
/// </summary>
/// <typeparam name="TCache">The caching implementation under test</typeparam>
public abstract class TextCacheTestBase<TCache> where TCache : ITextCache
{
    [Theory(DisplayName = "Text Cache: Contains Not"), CacheTest]
    public void NotContains(TCache cache, string key)
    {
        cache.Contains(key)
            .Should().BeFalse();
    }

    [Theory(DisplayName = "Text Cache: Contains"), CacheTest]
    public void Contains(TCache cache, string key, string value)
    {
        cache.SetString(key, value);

        cache.Contains(key)
            .Should().BeTrue();
    }

    [Theory(DisplayName = "Text Cache: Set/Get"), CacheTest]
    public void SetGet(TCache cache, string key, string value)
    {
        cache.SetString(key, value);

        cache.GetString(key)
            .Should().Be(value);
    }

    [Theory(DisplayName = "Text Cache: Set Twice"), CacheTest]
    public void SetTwice(TCache cache, string key, string value1, string value2)
    {
        // Ensure the cache handles overwriting existing values
        cache.SetString(key, value1);
        cache.SetString(key, value2);

        cache.GetString(key)
            .Should().Be(value2);
    }

    [Theory(DisplayName = "Text Cache: Get Missing returns Default"), CacheTest]
    public void GetMissing(TCache cache, string key)
    {
        cache.GetString(key)
            .Should().Be(default);
    }

    [Theory(DisplayName = "Text Cache: Location returns a string"), CacheTest]
    public void GettingLocation(TCache cache, string key1, string key2)
    {
        // Location is implementation-specific so this is about all we can assert generally
        var location1 = cache.GetLocation(key1);
        var location2 = cache.GetLocation(key2);

        location1.Should().NotBeNullOrEmpty();
        location2.Should().NotBeNullOrEmpty();
        location1.Should().NotBe(location2);
    }

    [Theory(DisplayName = "Text Cache: Set to Default prohibited"), CacheTest]
    public void SetDefault(TCache cache, string key)
    {
        Action act = () => cache.SetString(key, default!);
        act.Should().Throw<ArgumentException>("Default values are not permitted*");
    }

    [Theory(DisplayName = "Text Cache: Relative Expiration"), CacheTest]
    public void ExpiresAfterRelativeTimeElapsed(TCache subject)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4)
        };
        const string key = "the-key";
        const string value = "the-value";
        subject.SetString(key, value, options);

        subject.GetString(key).Should().Be(value);
        Thread.Sleep(1000);
        subject.GetString(key).Should().Be(value);
        Thread.Sleep(3500);

        // Check `Contains` first because that's where expiration should be implemented
        subject.Contains(key).Should().BeFalse();
        subject.GetString(key).Should().BeNull();
    }

    [Theory(DisplayName = "Text Cache: Update After Expiration"), CacheTest]
    public void UpdateAfterExpiration(TCache subject)
    {
        // This scenario arose because using the Created time of a file is not
        // a reliable way to know when the entry was last updated.
        // If a file is overwritten, or even if it is deleted then immediately re-written
        // the file will retain the original created time.
        // The net result can be a cache entry that is perpetually expired!
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
        };
        const string key = "the-key";
        const string value = "the-value";
        subject.SetString(key, "original value", options);

        // Wait for the entry to be expired
        Thread.Sleep(3000);
        // Then _update_ the value without fetching it
        subject.SetString(key, value, options);

        // The entry should not be expired because it has been replaced
        // so the clock should have been reset on its expiration
        subject.Contains(key).Should().BeTrue();
        subject.GetString(key).Should().Be(value);
    }
}
