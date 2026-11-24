using ThoughtStuff.Caching.FileSystem;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public class LocalFileCacheManagerTest : TextCacheManagerTestBase<LocalFileCache>
{
    [Theory(DisplayName = "Filesystem cache: Wildcard removal deletes payload and metadata"), CacheTest]
    public async Task WildcardRemoval(LocalFileCache cache)
    {
        cache.SetString("match.first", "value");
        File.WriteAllText(cache.GetLocation("match.legacy"), "legacy value");
        cache.SetString("keep", "other value");

        var removedCount = await cache.GetCacheManager().DeleteMatchingEntries("match.*");

        removedCount.Should().Be(2);
        Directory.EnumerateFiles(cache.BaseDirectory).Select(Path.GetFileName)
                 .Should().BeEquivalentTo("keep.txt", "keep.meta");
    }
}
