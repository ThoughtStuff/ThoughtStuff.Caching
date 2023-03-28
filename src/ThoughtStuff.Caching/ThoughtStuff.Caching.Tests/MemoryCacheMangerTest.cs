// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class MemoryCacheMangerTest
{
    // TODO: These tests are similar to TextCacheManagerTestBase

    [Theory(DisplayName = "MemoryCache Mgmt: Entry Count"), MemCacheTest]
    public async Task EntryCount(MemoryCacheTypedCache cache)
    {
        var subject = cache.GetCacheManager();
        const int count = 12;
        for (int i = 0; i < count; i++)
        {
            cache.Set(i.ToString(), i);
        }

        (await subject.GetCacheEntryCount()).Should().Be(count);
    }

    [Theory(DisplayName = "Caching Mgmt: Enumerate Keys"), MemCacheTest]
    public void EnumeratingKeys(MemoryCacheTypedCache cache)
    {
        const int count = 7;
        var expected = Enumerable.Range(0, count)
                                 .Select(i => i.ToString());
        foreach (var key in expected)
        {
            cache.Set(key, string.Empty);
        }
        var cacheManager = cache.GetCacheManager();

        var keys = cacheManager.EnumerateKeys().ToEnumerable();

        keys.Should().BeEquivalentTo(expected);
    }
}
