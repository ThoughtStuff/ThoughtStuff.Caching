using Microsoft.Extensions.Logging.Abstractions;
using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public sealed class LocalFileCacheCatalogTest : IDisposable
{
    private readonly string directory;
    private readonly LocalFileCacheDirectory directoryState;
    private readonly LocalFileCacheCatalog catalog;

    /// <summary>Creates an isolated SQLite catalog for each test.</summary>
    public LocalFileCacheCatalogTest()
    {
        directory = GetTemporaryDirectory();
        directoryState = new(directory);
        catalog = new(directoryState, NullLogger.Instance);
    }

    [Fact(DisplayName = "Filesystem catalog: A missing entry has zero cataloged bytes")]
    public void MissingEntry()
    {
        // The test calls the catalog directly, so it supplies the gate normally held by LocalFileCache.
        lock (directoryState.Gate)
        {
            catalog.Reconcile([]);

            var entrySizeBytes = catalog.GetEntrySizeBytes("absent.txt");

            entrySizeBytes.Should().Be(0);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Replacing an entry adjusts the total by its size difference")]
    public void ReplaceSize()
    {
        // Hold the gate across the replacement and size checks to follow the catalog's locking contract.
        lock (directoryState.Gate)
        {
            var retained = Entry("retained.txt");
            var replacement = Entry("replace.txt", entryVersion: "new", payloadSizeBytes: 140);
            catalog.Reconcile([retained, Entry(replacement.Path)]);

            catalog.Store(replacement);

            catalog.GetEntrySizeBytes(replacement.Path).Should().Be(replacement.TotalSizeBytes);
            catalog.TotalSizeBytes.Should().Be(retained.TotalSizeBytes + replacement.TotalSizeBytes);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Removing an entry subtracts its payload and metadata sizes")]
    public void RemoveSize()
    {
        // Hold the gate across removal and the size checks to follow the catalog's locking contract.
        lock (directoryState.Gate)
        {
            var retained = Entry("retained.txt");
            var removed = Entry("removed.txt");
            catalog.Reconcile([retained, removed]);

            catalog.Remove(removed.Path);

            catalog.GetEntrySizeBytes(removed.Path).Should().Be(0);
            catalog.TotalSizeBytes.Should().Be(retained.TotalSizeBytes);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Reconciliation removes rows absent from the latest inventory")]
    public void ReconcileInventory()
    {
        // Hold the gate across both inventories so their row and byte-count updates stay synchronized.
        lock (directoryState.Gate)
        {
            var retained = Entry("retained.txt");
            var missing = Entry("missing.txt");
            catalog.Reconcile([retained, missing]);

            catalog.Reconcile([retained]);

            catalog.GetEntrySizeBytes(missing.Path).Should().Be(0);
            catalog.TotalSizeBytes.Should().Be(retained.TotalSizeBytes);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: An interrupted inventory rolls back rows already written by the scan")]
    public void InventoryRollback()
    {
        // Hold the gate across the interrupted scan and the checks of its rolled-back state.
        lock (directoryState.Gate)
        {
            var existing = Entry("entry.txt");
            var changed = Entry(existing.Path, entryVersion: "new", payloadSizeBytes: 140);
            catalog.Reconcile([existing]);

            var failure = Record.Exception(() => catalog.Reconcile(InterruptAfter(changed)));

            failure.Should().BeOfType<IOException>();
            catalog.GetEntrySizeBytes(existing.Path).Should().Be(existing.TotalSizeBytes);
            catalog.TotalSizeBytes.Should().Be(existing.TotalSizeBytes);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: The expiry page includes unknown deadlines and excludes the incoming entry")]
    public void ExpiryPage()
    {
        // Hold the gate while building the inventory and reading the resulting candidate page.
        lock (directoryState.Gate)
        {
            var cutoff = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var expired = Entry("expired.txt", expiresAtUtc: cutoff.Ticks - 1);
            var unknown = Entry("unknown.txt");
            var unexpired = Entry("unexpired.txt", expiresAtUtc: cutoff.Ticks + 1);
            var incoming = Entry("incoming.txt", expiresAtUtc: cutoff.Ticks - 1);
            catalog.Reconcile([expired, unknown, unexpired, incoming]);

            var paths = CandidatePaths(incoming.Path, cutoff);

            paths.Should().BeEquivalentTo(expired.Path, unknown.Path);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Reconciliation preserves recorded recency for an unchanged version")]
    public void PreserveRecency()
    {
        // Keep the timer out while recording and flushing the hit, then check that reconciliation preserves it.
        lock (directoryState.Gate)
        {
            var older = Entry("older.txt", publishedAtUtc: 100);
            var newer = Entry("newer.txt", publishedAtUtc: 200);
            catalog.Reconcile([older, newer]);
            catalog.RecordHit(older);
            catalog.FlushHits();

            catalog.Reconcile([older, newer]);

            CandidatePaths().Should().Equal(newer.Path, older.Path);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Replacing an entry clears the prior version's recorded recency")]
    public void ResetRecency()
    {
        // Keep the timer out while recording the hit and replacing the entry to control the order.
        lock (directoryState.Gate)
        {
            var previous = Entry("entry.txt", publishedAtUtc: 100);
            var replacement = Entry(previous.Path, entryVersion: "new", publishedAtUtc: 200);
            var newer = Entry("newer.txt", publishedAtUtc: 300);
            catalog.Reconcile([previous, newer]);
            catalog.RecordHit(previous);
            catalog.FlushHits();

            catalog.Store(replacement);

            CandidatePaths().Should().Equal(replacement.Path, newer.Path);
        }
    }

    [Fact(DisplayName = "Filesystem catalog: Flushing a stale hit leaves a replacement's recency alone")]
    public void StaleHit()
    {
        // Keep the timer out so the test explicitly flushes the stale hit before checking recency.
        lock (directoryState.Gate)
        {
            var replacement = Entry("entry.txt", entryVersion: "new", publishedAtUtc: 200);
            var newer = Entry("newer.txt", publishedAtUtc: 300);
            catalog.Reconcile([replacement, newer]);
            catalog.RecordHit(Entry(replacement.Path));

            catalog.FlushHits();

            CandidatePaths().Should().Equal(replacement.Path, newer.Path);
        }
    }

    private static CacheFileEntry Entry(string path,
                                        string entryVersion = "original",
                                        long payloadSizeBytes = 100,
                                        long publishedAtUtc = 100,
                                        long? expiresAtUtc = null) =>
        new(path, entryVersion, payloadSizeBytes, metadataSizeBytes: 20, publishedAtUtc, expiresAtUtc);

    private IEnumerable<string> CandidatePaths(string incomingPath = "incoming.txt", DateTimeOffset? expiredAt = null) =>
        catalog.ReadCandidates(incomingPath, expiredAt, offset: 0, pageSize: 10).Select(entry => entry.Path);

    private static IEnumerable<CacheFileEntry> InterruptAfter(CacheFileEntry entry)
    {
        yield return entry;
        throw new IOException("Interrupted inventory");
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
