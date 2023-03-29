// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public class LocalFileCacheTest
{
    [Theory(DisplayName = "Caching: File name"), AutoMoq]
    public void KeyTest(LocalFileCache localFileCache)
    {
        // Note that illegal chars vary by OS
        localFileCache.GetFilePath(@" a &/b(c:d)\e ")
                      .Should().EndWith("a &_b(c_d)_e.txt");
    }

    [Theory(DisplayName = "Caching: Reserved File Names")]
    [InlineData("..")]
    [InlineData("con")]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("nul")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM2")]
    [InlineData("COM3")]
    [InlineData("COM4")]
    [InlineData("COM5")]
    [InlineData("COM6")]
    [InlineData("COM7")]
    [InlineData("COM8")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT2")]
    [InlineData("LPT3")]
    [InlineData("LPT4")]
    [InlineData("LPT5")]
    [InlineData("LPT6")]
    [InlineData("LPT7")]
    [InlineData("LPT8")]
    [InlineData("LPT9")]
    [InlineData("GLOBALROOT")]
    public void ReservedNames(string key)
    {
        var fileName = LocalFileCache.GetFileName(key);
        fileName.Should().NotBe(key)
            .And.NotBe($"{key}.")
            .And.NotBe($"{key}.txt");
        var temp = GetTemporaryDirectory();
        // Validate can create file
        File.WriteAllText(Path.Combine(temp, fileName), "");
    }

    //[Theory(DisplayName = "Caching: Directory"), AutoMoq]
    //public void ShouldUseUserDirectory(LocalFileCache subject)
    //{
    //    subject.BaseDirectory.Should().BeOneOf(
    //        @"C:\Users\jfoshee\ThoughtStuff\LocalFileCache",
    //        "/Users/jfoshee/ThoughtStuff/LocalFileCache");
    //}

    [Theory(DisplayName = "Caching: Location"), AutoMoq]
    public void Locator(LocalFileCache localFileCache)
    {
        var key = "the-key";
        var baseDirectory = localFileCache.BaseDirectory + Path.DirectorySeparatorChar;

        var location = localFileCache.GetLocation(key);

        location.Should().Be($"{baseDirectory}the-key.txt");
    }

    [Theory(DisplayName = "Caching: Creata metadata file"), CacheTest]
    public void CreatesMetadataFile(LocalFileCache subject)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(new DateTime(2000, 5, 12)),
            AbsoluteExpirationRelativeToNow = new TimeSpan(4, 3, 2),
            //SlidingExpiration = new TimeSpan(9, 8, 7)
        };

        subject.SetString("the-key", "the-value", options);

        var baseDirectory = subject.BaseDirectory;
        File.Exists(Path.Combine(baseDirectory, "the-key.txt")).Should().BeTrue();
        string metaPath = Path.Combine(baseDirectory, "the-key.meta");
        File.Exists(metaPath).Should().BeTrue();
        var metaText = File.ReadAllText(metaPath);
        var metadata = JsonSerializer.Deserialize<LocalFileCacheMetadata>(metaText)!;
        metadata.CacheEntryOptions.Should().BeEquivalentTo(options);
    }

    [Theory(DisplayName = "Caching: Relative Expiration"), CacheTest]
    public void ExpiresAfterRelativeTimeElapsed(LocalFileCache subject)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, 4)
        };
        const string key = "the-key";
        const string value = "the-value";
        subject.SetString(key, value, options);

        subject.GetString(key).Should().Be(value);
        Thread.Sleep(1000);
        subject.GetString(key).Should().Be(value);
        Thread.Sleep(3500);
        subject.GetString(key).Should().BeNull();

        var baseDirectory = subject.BaseDirectory;
        File.Exists(Path.Combine(baseDirectory, "the-key.txt")).Should().BeFalse();
        var metaPath = Path.Combine(baseDirectory, "the-key.meta");
        File.Exists(metaPath).Should().BeFalse();
    }

    [Theory(DisplayName = "Caching: Expiration for Contains"), CacheTest]
    public void HandleExpirationForContains(LocalFileCache subject)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, 4)
        };
        const string key = "the-key";
        const string value = "the-value";
        subject.SetString(key, value, options);

        subject.Contains(key).Should().BeTrue();
        Thread.Sleep(1000);
        subject.Contains(key).Should().BeTrue();
        Thread.Sleep(3500);
        subject.Contains(key).Should().BeFalse();

        var baseDirectory = subject.BaseDirectory;
        File.Exists(Path.Combine(baseDirectory, "the-key.txt")).Should().BeFalse();
        var metaPath = Path.Combine(baseDirectory, "the-key.meta");
        File.Exists(metaPath).Should().BeFalse();
    }

    [Theory(DisplayName = "Caching: Metadata missing is Unexpired"), AutoMoq]
    public void UnexpiredWithoutMetadata([Frozen] Mock<ICacheExpirationService> cacheExpirationService,
                                         [Frozen] Mock<IObjectFileSerializer> objectFileSerializer,
                                         LocalFileCache subject,
                                         bool isExpired,
                                         string metadataPath,
                                         DateTime updatedTime)
    {
        // Setup: the metadata file does not exist
        objectFileSerializer.Setup(s => s.DeserializeFromFile<LocalFileCacheMetadata>(metadataPath))
            .Throws<FileNotFoundException>();
        // So null cache options should be passed to the cacheExpirationService
        cacheExpirationService.Reset();
        cacheExpirationService.Setup(s => s.IsExpired(null, updatedTime))
            .Returns(isExpired);

        subject.IsExpired(metadataPath, updatedTime)
            .Should().Be(isExpired);

        cacheExpirationService.VerifyAll();
    }
}
