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

    [Theory(DisplayName = "Caching Mgmt: Matching Entry Count"), MemCacheTest]
    public async Task MgmtMatchingEntryCountAsync(MemoryCacheTypedCache cache)
    {
        var keys = new[]
        {
            "Alphabet",
            "alpha",
            "beta",
            "gamma",
            "alphabeta",
            "betta",
            // Keys containing regex chars
            @"C:\woo",
            @"[a]",
            // Keys containing wildcards
            "a*b*c",
            "gamm?",
            // Keys problematic for file or blob storage
            "period.",
            "trailing/",
            "../doubledot",
            "NUL",
        };
        foreach (var key in keys)
        {
            cache.Set(key, string.Empty);
        }
        var cacheManager = cache.GetCacheManager();

        var expectations = new[]
        {
            // No matches
            ("a", 0),
            ("b", 0),
            ("zeta", 0),
            // Exact matches
            ("alpha", 1),
            ("beta", 1),
            ("gamma", 1),
            // Leading/trailing wildcards
            ("Alpha*", 1),
            ("Alpha?", 0),
            ("*lpha*", 3),
            ("?lpha*", 3),
            ("*bet*", 4),
            ("*a*", 10),
            // Interior wildcards
            ("bet*a", 2),
            // Question mark matches single character
            ("*bet?", 2),
            ("be?a", 1),
            ("bet?a", 1),
            ("?????", 5),
            ("C*w??", 1),
            // Wildcards matching 0 chars
            ("*gamma*", 1),
            // Handle Regex Control Characters
            (@"C:\woo", 1),
            (@"C:\w*", 1),
            (@"*\w*", 1),
            (@"[a]", 1),
            (@"a.*", 0),
            // Other special chars
            ("a?b?c", 1),
            ("gamm?", 2),
            ("period", 0),
            ("period.", 1),
            ("period?", 1),
            ("period*", 1),
            ("trailing", 0),
            ("trailing/", 1),
            ("trailing?", 1),
            ("../doubledot", 1),
            ("NUL", 1)
        };
        foreach (var (keyExpression, expectedCount) in expectations)
        {
            (await cacheManager.GetCountOfMatchingEntries(keyExpression))
                .Should().Be(expectedCount, $"'{keyExpression}'");
        }
    }

    [Theory(DisplayName = "Caching Mgmt: Deleting Matching Entries"), MemCacheTest]
    public async Task MgmtDeleteMatchingEntriesAsync(MemoryCacheTypedCache cache)
    {
        var keys = new[]
        {
            "wocket",
            "pocket",
            "bofa",

            "zellar",
            "sofa",
            "gellar",

            "zillow",
            "cellar",
            "dellar"
        };
        foreach (var key in keys)
        {
            cache.Set(key, string.Empty);
        }
        var cacheManager = cache.GetCacheManager();

        (await cacheManager.GetCacheEntryCount())
            .Should().Be(9);

        (await cacheManager.DeleteMatchingEntries("sofa"))
            .Should().Be(1);
        (await cacheManager.GetCacheEntryCount())
            .Should().Be(8);

        (await cacheManager.DeleteMatchingEntries("?ocket"))
            .Should().Be(2);
        (await cacheManager.GetCacheEntryCount())
            .Should().Be(6);

        (await cacheManager.DeleteMatchingEntries("*llar"))
            .Should().Be(4);
        (await cacheManager.GetCacheEntryCount())
            .Should().Be(2);
    }
}
