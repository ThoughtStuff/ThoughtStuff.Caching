// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class DictionaryTextCacheTest : TextCacheTestBase<DictionaryTextCache>
{
    [Theory(DisplayName = "Caching Dictionay: Location Matches Key"), AutoMoq]
    public void Location(DictionaryTextCache cache, string key)
    {
        cache.GetLocation(key).Should().Be(key);
    }
}
