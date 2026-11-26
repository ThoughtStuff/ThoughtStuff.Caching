using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public sealed class LocalFileCacheCapacityTest : IDisposable
{
    private const int Limit = 1024;
    private static readonly string Payload = new('x', 300);
    private readonly string directory = GetTemporaryDirectory();

    public interface ILargeService
    {
        string Read();
    }

    public sealed class LargeService : ILargeService
    {
        public string Read() => new('x', Limit + 1);
    }

    [Fact(DisplayName = "Filesystem capacity: Create the catalog from a legacy payload")]
    public void LegacyInventory()
    {
        var cache = CreateCache();
        File.WriteAllText(cache.GetLocation("legacy"), Payload);

        var value = cache.GetString("legacy");

        value.Should().Be(Payload);
        ReadCatalog<long>("SELECT payload_size_bytes FROM cache_entries").Should().Be(Payload.Length);
    }

    [Fact(DisplayName = "Filesystem capacity: Rebuild a missing catalog with the same entry version")]
    public void RebuildCatalog()
    {
        var cache = CreateCache();
        cache.SetString("entry", Payload);
        var entryVersion = ReadCatalog<string>("SELECT entry_version FROM cache_entries");
        File.Delete(Path.Combine(directory, LocalFileCacheCatalog.FileName));

        cache.Contains("entry").Should().BeTrue();

        ReadCatalog<string>("SELECT entry_version FROM cache_entries").Should().Be(entryVersion);
    }

    [Fact(DisplayName = "Filesystem capacity: A hit through another instance protects the entry from LRU")]
    public void SharedRecency()
    {
        var cache = CreateCache();
        var otherCache = CreateCache(Path.Combine(directory, "."));
        cache.SetString("oldest", Payload);
        cache.SetString("untouched", Payload);
        otherCache.GetString("oldest");

        cache.SetString("new", Payload);

        File.Exists(cache.GetLocation("oldest")).Should().BeTrue();
        File.Exists(cache.GetLocation("untouched")).Should().BeFalse();
        File.Exists(cache.GetLocation("new")).Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem capacity: Persist buffered recency without a subsequent write")]
    public async Task PersistedHit()
    {
        var cache = CreateCache();
        cache.SetString("entry", Payload);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));

        cache.GetString("entry");
        object timestamp;
        while ((timestamp = ReadCatalog<object>("SELECT last_used_at_utc FROM cache_entries")) is DBNull)
            await Task.Delay(100, deadline.Token);

        timestamp.Should().BeOfType<long>().Which.Should().BePositive();
    }

    [Fact(DisplayName = "Filesystem capacity: A successful payload read counts when deserialization fails")]
    public void InvalidJsonHit()
    {
        var cache = CreateCache();
        var typedCache = new JsonTypedCache(cache);
        cache.SetString("invalid", Payload);
        cache.SetString("untouched", Payload);
        Action read = () => typedCache.Get<int>("invalid");
        read.Should().Throw<System.Text.Json.JsonException>();

        cache.SetString("new", Payload);

        File.Exists(cache.GetLocation("invalid")).Should().BeTrue();
        File.Exists(cache.GetLocation("untouched")).Should().BeFalse();
    }

    [Fact(DisplayName = "Filesystem capacity: Remove an expired entry before an older unexpired entry")]
    public void ExpiredFirst()
    {
        var cache = CreateCache();
        cache.SetString("live", Payload);
        cache.SetString("expired", Payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        cache.SetString("new", Payload);

        File.Exists(cache.GetLocation("live")).Should().BeTrue();
        File.Exists(cache.GetLocation("expired")).Should().BeFalse();
        File.Exists(cache.GetLocation("new")).Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem capacity: Bypass an oversized write without evicting existing entries")]
    public void OversizedWrite()
    {
        var cache = CreateCache();
        cache.SetString("keep", Payload);

        cache.SetString("oversized", new string('x', Limit + 1));

        File.Exists(cache.GetLocation("oversized")).Should().BeFalse();
        File.Exists(cache.GetLocation("keep")).Should().BeTrue();
    }

    [Fact(DisplayName = "Filesystem capacity: A normal write evicts at most one batch from an oversized cache")]
    public void WriteBudget()
    {
        var cache = CreateCache();
        for (var i = 0; i < 40; ++i)
            File.WriteAllText(cache.GetLocation($"legacy-{i}"), Payload);

        cache.SetString("new", Payload);

        Directory.EnumerateFiles(directory, "*.txt").Should().HaveCount(8);
        File.Exists(cache.GetLocation("new")).Should().BeFalse();
    }

    [Fact(DisplayName = "Filesystem capacity: The method proxy returns a value that cannot be cached")]
    public void ProxyBypass()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddMethodCaching()
            .AddLocalFileTextCache(options =>
            {
                options.BaseDirectory = directory;
                options.SoftSizeLimitBytes = Limit;
            })
            .AddTransientWithCaching<ILargeService, LargeService, string>()
            .BuildServiceProvider();
        var proxy = services.GetRequiredService<ILargeService>();

        var value = proxy.Read();

        value.Should().HaveLength(Limit + 1);
        Directory.EnumerateFiles(directory, "*.txt").Should().BeEmpty();
    }

    [Fact(DisplayName = "Filesystem capacity: Catalog paths follow filesystem case matching")]
    public void CaseMatching()
    {
        var cache = CreateCache();
        cache.SetString("Entry", Payload);
        var sameFile = File.Exists(cache.GetLocation("entry"));

        cache.SetString("entry", Payload);

        ReadCatalog<long>("SELECT COUNT(*) FROM cache_entries").Should().Be(sameFile ? 1 : 2);
    }

    [Fact(DisplayName = "Filesystem capacity: Leave a changed publication alone when the candidate is stale")]
    public void StaleCandidate()
    {
        var cache = CreateCache();
        cache.SetString("replace", Payload);
        cache.SetString("keep", Payload);
        File.WriteAllText(cache.GetLocation("replace"), Payload + " replacement");

        cache.SetString("new", Payload);

        File.ReadAllText(cache.GetLocation("replace")).Should().EndWith(" replacement");
    }

    [Fact(DisplayName = "Filesystem capacity: Remove the catalog row when wildcard deletion removes a pair")]
    public async Task WildcardAccounting()
    {
        var cache = CreateCache();
        cache.SetString("remove", Payload);
        var manager = cache.GetCacheManager();

        await manager.DeleteMatchingEntries("rem*");

        ReadCatalog<long>("SELECT COUNT(*) FROM cache_entries").Should().Be(0);
    }

    private LocalFileCache CreateCache(string? cacheDirectory = null) =>
        new(new JsonFileSerializer(),
            new CacheExpirationService(new HardCodedDefaultCachePolicy(), NullLogger<CacheExpirationService>.Instance),
            Options.Create(new LocalFileCacheOptions
            {
                BaseDirectory = cacheDirectory ?? directory,
                SoftSizeLimitBytes = Limit
            }));

    private T ReadCatalog<T>(string sql)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, LocalFileCacheCatalog.FileName),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(connectionString.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
