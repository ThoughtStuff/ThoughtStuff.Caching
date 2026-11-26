using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public sealed class LocalFileCacheTrimTest : IDisposable
{
    private static readonly TimeSpan MaintenanceWindow = TimeSpan.FromSeconds(30);
    private readonly string directory = GetTemporaryDirectory();
    private readonly LocalFileCacheOptions options = new() { SoftSizeLimitBytes = 1024 };

    [Fact(DisplayName = "Filesystem trim: Evict beyond the normal write batch until the configured limit is reached")]
    public void MultipleBatches()
    {
        options.SoftSizeLimitBytes = 4;
        var cache = CreateCache();
        for (var i = 0; i < 40; ++i)
            PublishLegacy(cache, $"entry-{i}");

        cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        Directory.EnumerateFiles(directory, "*.txt")
                 .Sum(path => new FileInfo(path).Length)
                 .Should().Be(4);
    }

    [Fact(DisplayName = "Filesystem trim: Keep entries when their combined size is already at the limit")]
    public void ExactLimit()
    {
        options.SoftSizeLimitBytes = 1;
        var cache = CreateCache();
        var path = PublishLegacy(cache, "entry");

        cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        File.Exists(path).Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem trim: Count companion metadata and remove it together with the payload")]
    public void CompanionBytes()
    {
        options.SoftSizeLimitBytes = 1;
        var cache = CreateCache();
        var path = PublishLegacy(cache, "entry");
        var metadataPath = Path.ChangeExtension(path, ".meta");
        File.WriteAllText(metadataPath, "{}");

        cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        File.Exists(path).Should().BeFalse();
        File.Exists(metadataPath).Should().BeFalse();
    }

    [Fact(DisplayName = "Filesystem trim: Evict an expired entry before an older live entry")]
    public void ExpiredFirst()
    {
        var cache = CreateCache();
        cache.SetString("live", "x", new() { AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1) });
        cache.SetString("expired", "x", new() { AbsoluteExpiration = DateTimeOffset.UnixEpoch });
        var livePath = cache.GetLocation("live");
        File.SetLastWriteTimeUtc(livePath, DateTime.UtcNow.AddDays(-1));
        options.SoftSizeLimitBytes = new FileInfo(livePath).Length +
                                     new FileInfo(Path.ChangeExtension(livePath, ".meta")).Length;

        cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        Directory.EnumerateFiles(directory, "*.txt").Should().Equal(livePath);
    }

    [Fact(DisplayName = "Filesystem trim: A recent read protects the older publication from LRU eviction")]
    public void RecentHit()
    {
        options.SoftSizeLimitBytes = 1;
        var cache = CreateCache();
        var otherCache = CreateCache();
        var read = PublishLegacy(cache, "read");
        var unread = PublishLegacy(cache, "unread");
        File.SetLastWriteTimeUtc(read, DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(unread, DateTime.UtcNow.AddMinutes(-1));
        var gate = LocalFileCacheDirectory.Get(directory).Gate;
        // Keep the timer out so maintenance has to flush the buffered hit itself.
        lock (gate)
        {
            otherCache.GetString("read");

            cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

            Directory.EnumerateFiles(directory, "*.txt").Should().Equal(read);
        }
    }

    [Fact(DisplayName = "Filesystem trim: Refresh a warm catalog to include a payload published before maintenance")]
    public void RefreshInventory()
    {
        options.SoftSizeLimitBytes = 1;
        var cache = CreateCache();
        cache.Contains("absent");
        PublishLegacy(cache, "added", "oversized");

        cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        Directory.EnumerateFiles(directory, "*.txt").Should().BeEmpty();
    }

    [Fact(DisplayName = "Filesystem trim: An uncapped instance uses the configured limit shared by its directory")]
    public void SharedLimit()
    {
        options.SoftSizeLimitBytes = 1;
        var configuredCache = CreateCache();
        configuredCache.Contains("absent");
        PublishLegacy(configuredCache, "added", "oversized");
        var uncappedCache = new LocalFileCache(new JsonFileSerializer(),
                                              DefaultExpiration(),
                                              Options.Create(new LocalFileCacheOptions { BaseDirectory = directory }));

        uncappedCache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken);

        Directory.EnumerateFiles(directory, "*.txt").Should().BeEmpty();
    }

    [Fact(DisplayName = "Filesystem trim: Reject maintenance on a directory without a configured size limit")]
    public void MissingLimit()
    {
        options.SoftSizeLimitBytes = null;
        var cache = CreateCache();

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Theory(DisplayName = "Filesystem trim: Reject a non-positive maintenance window")]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidTimeout(int milliseconds)
    {
        var cache = CreateCache();

        var error = Record.Exception(() => cache.TrimToSize(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken));

        error.Should().BeOfType<ArgumentOutOfRangeException>()
             .Which.ParamName.Should().Be("timeout");
    }

    [Fact(DisplayName = "Filesystem trim: The maintenance timeout includes time waiting for the directory lock")]
    public async Task LockTimeout()
    {
        var cache = CreateCache();
        var gate = LocalFileCacheDirectory.Get(directory).Gate;
        var testToken = TestContext.Current.CancellationToken;
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Task.Run(() =>
        {
            lock (gate)
            {
                entered.SetResult();
                release.Wait(testToken);
            }
        }, testToken);
        Exception? error;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), testToken);
            error = await Task.Run(() => Record.Exception(() => cache.TrimToSize(TimeSpan.FromMilliseconds(50), testToken)),
                                   testToken)
                              .WaitAsync(TimeSpan.FromSeconds(5), testToken);
        }
        finally
        {
            release.Set();
            await holder;
        }

        error.Should().BeOfType<TimeoutException>();
    }

    [Fact(DisplayName = "Filesystem trim: A canceled caller token prevents starting the inventory")]
    public void CanceledBeforeStart()
    {
        var cache = CreateCache();
        var canceled = new CancellationToken(canceled: true);

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, canceled));

        error.Should().BeOfType<OperationCanceledException>()
             .Which.CancellationToken.Should().Be(canceled);
        File.Exists(Path.Combine(directory, LocalFileCacheCatalog.FileName)).Should().BeFalse();
    }

    [Fact(DisplayName = "Filesystem trim: Cancellation during inventory leaves the catalog marked for reconciliation")]
    public void CanceledDuringInventory()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        options.SoftSizeLimitBytes = 0;
        var cache = CreateCache(serializer: new CancelOnReadSerializer(caller));
        var path = PublishLegacy(cache, "entry");
        File.WriteAllText(Path.ChangeExtension(path, ".meta"), "{}");

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, caller.Token));

        error.Should().BeOfType<OperationCanceledException>()
             .Which.CancellationToken.Should().Be(caller.Token);
        File.Exists(path).Should().BeTrue();
        LocalFileCacheDirectory.Get(directory).Catalog!.NeedsReconciliation.Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem trim: Cancellation keeps the completed eviction and stops before the next deletion")]
    public void CanceledBetweenEvictions()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        options.SoftSizeLimitBytes = 0;
        var expiration = new Mock<ICacheExpirationService>();
        var cache = CreateCache(expiration.Object);
        var first = PublishLegacy(cache, "a");
        var second = PublishLegacy(cache, "b");
        expiration.Setup(policy => policy.IsExpired(It.IsAny<DistributedCacheEntryOptions?>(), It.IsAny<DateTimeOffset>()))
                  .Returns(() =>
                  {
                      if (!File.Exists(first))
                          caller.Cancel();
                      return true;
                  });

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, caller.Token));

        error.Should().BeOfType<OperationCanceledException>()
             .Which.CancellationToken.Should().Be(caller.Token);
        Directory.EnumerateFiles(directory, "*.txt").Should().Equal(second);
    }

    [Fact(DisplayName = "Filesystem trim: A failed deletion leaves later entries intact and flags the catalog for repair")]
    public void FailedDeletion()
    {
        options.SoftSizeLimitBytes = 0;
        var cache = CreateCache();
        var first = PublishLegacy(cache, "a");
        var blocked = PublishLegacy(cache, "b");
        var last = PublishLegacy(cache, "c");
        File.SetLastWriteTimeUtc(first, DateTime.UnixEpoch.AddDays(1));
        File.SetLastWriteTimeUtc(blocked, DateTime.UnixEpoch.AddDays(2));
        File.SetLastWriteTimeUtc(last, DateTime.UnixEpoch.AddDays(3));
        // File.Delete cannot remove a directory, regardless of the account's permissions.
        Directory.CreateDirectory(Path.ChangeExtension(blocked, ".meta"));

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken));

        error.Should().Match<Exception>(exception => exception is UnauthorizedAccessException || exception is IOException);
        Directory.EnumerateFiles(directory, "*.txt").Should().Equal(last);
        LocalFileCacheDirectory.Get(directory).Catalog!.NeedsReconciliation.Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem trim: Report a publication changed during maintenance without deleting it")]
    public void ChangedPublication()
    {
        options.SoftSizeLimitBytes = 0;
        var expiration = new Mock<ICacheExpirationService>();
        var cache = CreateCache(expiration.Object);
        var first = PublishLegacy(cache, "a");
        var changed = PublishLegacy(cache, "b");
        expiration.Setup(policy => policy.IsExpired(It.IsAny<DistributedCacheEntryOptions?>(), It.IsAny<DateTimeOffset>()))
                  .Returns(() =>
                  {
                      File.WriteAllText(changed, "replacement");
                      return false;
                  });

        var error = Record.Exception(() => cache.TrimToSize(MaintenanceWindow, TestContext.Current.CancellationToken));

        error.Should().BeOfType<IOException>();
        File.Exists(first).Should().BeTrue();
        File.ReadAllText(changed).Should().Be("replacement");
    }

    private LocalFileCache CreateCache(ICacheExpirationService? expiration = null, IObjectFileSerializer? serializer = null)
    {
        options.BaseDirectory = directory;
        return new(serializer ?? new JsonFileSerializer(),
                   expiration ?? DefaultExpiration(),
                   Options.Create(options));
    }

    private static CacheExpirationService DefaultExpiration() =>
        new(new HardCodedDefaultCachePolicy(), NullLogger<CacheExpirationService>.Instance);

    private static string PublishLegacy(LocalFileCache cache, string key, string value = "x")
    {
        var path = cache.GetLocation(key);
        File.WriteAllText(path, value);
        return path;
    }

    private sealed class CancelOnReadSerializer(CancellationTokenSource caller) : IObjectFileSerializer
    {
        private readonly JsonFileSerializer serializer = new();

        /// <inheritdoc/>
        public T? DeserializeFromFile<T>(string path)
        {
            caller.Cancel();
            return serializer.DeserializeFromFile<T>(path);
        }

        /// <inheritdoc/>
        public void SerializeToFile<T>(string path, T value) => serializer.SerializeToFile(path, value);
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
