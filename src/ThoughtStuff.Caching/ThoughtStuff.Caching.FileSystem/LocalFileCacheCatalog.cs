using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ThoughtStuff.Caching.FileSystem;

/// <summary>
/// Indexes published cache files in SQLite and tracks the total size and recency of live entries.
/// Published files are the source of truth for rebuilding the catalog.
/// </summary>
/// <remarks>
/// Callers hold <see cref="LocalFileCacheDirectory.Gate"/> across file operations and their
/// catalog updates. The flush timer takes the same gate before touching the pending hit batch.
/// Multi-process support must revisit that ordering, the running byte count, and the hit buffer.
/// </remarks>
internal sealed class LocalFileCacheCatalog(LocalFileCacheDirectory directoryState, ILogger logger)
{
    internal const string FileName = ".thoughtstuff-cache.sqlite";
    internal const string StagingDirectoryName = ".thoughtstuff-cache-staging";
    private const string OwnerFileName = ".thoughtstuff-cache.owner";
    private const int MaxPendingHitCount = 256;
    private static readonly TimeSpan HitFlushInterval = TimeSpan.FromSeconds(5);

    private const string EntryColumns =
        "payload_path, entry_version, payload_size_bytes, metadata_size_bytes, " +
        "published_at_utc, expires_at_utc";

    // A path survives replacement. Its recorded recency is valid only for the same entry version.
    private const string UpsertSql = """
        INSERT INTO cache_entries (
            payload_path, entry_version, payload_size_bytes, metadata_size_bytes,
            published_at_utc, expires_at_utc
        )
        VALUES ($path, $version, $payload_size_bytes, $metadata_size_bytes, $published, $expires)
        ON CONFLICT(payload_path) DO UPDATE SET
            last_used_at_utc = CASE WHEN cache_entries.entry_version = excluded.entry_version
                                    THEN cache_entries.last_used_at_utc END,
            entry_version = excluded.entry_version,
            payload_size_bytes = excluded.payload_size_bytes,
            metadata_size_bytes = excluded.metadata_size_bytes,
            published_at_utc = excluded.published_at_utc,
            expires_at_utc = excluded.expires_at_utc
        """;

    private readonly string directory = directoryState.DirectoryPath;
    private readonly string databasePath = Path.Combine(directoryState.DirectoryPath, FileName);
    private readonly Dictionary<string, (string EntryVersion, long Timestamp)> pendingHits = new();
    private Timer? hitTimer;

    /// <summary>Whether to inventory files before using the catalog, including on first use.</summary>
    internal bool NeedsReconciliation { get; set; } = true;

    /// <summary>Running sum of the cataloged payload and metadata lengths in bytes.</summary>
    internal long TotalSizeBytes { get; private set; }

    /// <summary>Whether the database file is still present. A missing file triggers a rebuild.</summary>
    internal bool Exists => File.Exists(databasePath);

    /// <summary>Rebuilds or refreshes the index from disk while preserving recency for unchanged publications.</summary>
    internal void Reconcile(IEnumerable<CacheFileEntry> files)
    {
        using var connection = Open(writableCatalogExists: false);
        using var transaction = connection.BeginTransaction();
        using (var command = CreateCommand(connection, """
            CREATE TABLE IF NOT EXISTS cache_entries (
                payload_path        TEXT PRIMARY KEY NOT NULL, -- Relative path with filesystem casing rules.
                entry_version       TEXT NOT NULL,             -- Metadata token and file stamps.
                payload_size_bytes  INTEGER NOT NULL,          -- Payload file length in bytes.
                metadata_size_bytes INTEGER NOT NULL,          -- Companion length in bytes, or zero if absent.
                published_at_utc    INTEGER NOT NULL,          -- Payload write time in UTC ticks.
                expires_at_utc      INTEGER,                   -- Expiry in UTC ticks, or null if unknown.
                last_used_at_utc    INTEGER                    -- Last successful read in UTC ticks, or null.
            );
            CREATE INDEX IF NOT EXISTS cache_entries_expiration
                ON cache_entries (expires_at_utc, payload_path);
            CREATE INDEX IF NOT EXISTS cache_entries_recency
                ON cache_entries (COALESCE(last_used_at_utc, published_at_utc), payload_path);
            CREATE TEMP TABLE inventory_paths (
                payload_path TEXT PRIMARY KEY -- Relative payload path confirmed by the current scan.
            );
            """, transaction))
        {
            command.ExecuteNonQuery();
        }

        using var upsert = CreateCommand(connection, UpsertSql, transaction);
        using var inventory = CreateCommand(connection, "INSERT INTO inventory_paths VALUES ($path)", transaction);
        var inventoryPath = inventory.Parameters.Add("$path", SqliteType.Text);
        long totalSizeBytes = 0;
        // The temporary table records which rows the scan confirms so stale rows can be removed
        // in the same transaction. If enumeration fails, the original rows remain available.
        foreach (var entry in files)
        {
            SetEntryParameters(upsert, entry);
            upsert.ExecuteNonQuery();
            inventoryPath.Value = entry.Path;
            inventory.ExecuteNonQuery();
            totalSizeBytes += entry.TotalSizeBytes;
        }

        using (var command = CreateCommand(connection, """
            DELETE FROM cache_entries
            WHERE payload_path NOT IN (SELECT payload_path FROM inventory_paths)
            """, transaction))
        {
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        TotalSizeBytes = totalSizeBytes;
        NeedsReconciliation = false;
    }

    /// <summary>Gets the cataloged byte count for a relative payload path, or zero if absent.</summary>
    internal long GetEntrySizeBytes(string path)
    {
        using var connection = Open();
        return GetEntrySizeBytes(connection, path);
    }

    /// <summary>Indexes an already-published entry and applies the size difference after the database write.</summary>
    internal void Store(CacheFileEntry entry)
    {
        using var connection = Open();
        var previousSizeBytes = GetEntrySizeBytes(connection, entry.Path);
        using var command = CreateCommand(connection, UpsertSql);
        SetEntryParameters(command, entry);
        command.ExecuteNonQuery();
        TotalSizeBytes += entry.TotalSizeBytes - previousSizeBytes;
        if (pendingHits.TryGetValue(entry.Path, out var pending) && pending.EntryVersion != entry.EntryVersion)
            pendingHits.Remove(entry.Path);
    }

    /// <summary>Removes the index row and pending hits after the entry files have been deleted.</summary>
    internal void Remove(string path)
    {
        using var connection = Open();
        var previousSizeBytes = GetEntrySizeBytes(connection, path);
        using var command = CreateCommand(connection, "DELETE FROM cache_entries WHERE payload_path = $path");
        command.Parameters.AddWithValue("$path", path);
        command.ExecuteNonQuery();
        TotalSizeBytes -= previousSizeBytes;
        pendingHits.Remove(path);
    }

    /// <summary>Returns a page for expiry checks or LRU eviction, excluding the incoming entry's path.</summary>
    /// <remarks>
    /// Expiry hints can be absent, so the expiry pass returns rows with unknown deadlines too.
    /// The caller checks both the filesystem identity and the applicable expiry policy.
    /// </remarks>
    internal List<CacheFileEntry> ReadCandidates(string except,
                                                DateTimeOffset? expiredAt,
                                                long offset,
                                                int pageSize)
    {
        using var connection = Open();
        var expiryFilter = expiredAt.HasValue
            ? "AND (expires_at_utc <= $expires OR expires_at_utc IS NULL)"
            : "";
        var orderBy = expiredAt.HasValue
            ? "expires_at_utc, payload_path"
            : "COALESCE(last_used_at_utc, published_at_utc), payload_path";
        using var command = CreateCommand(connection, $"""
            SELECT {EntryColumns} FROM cache_entries
            WHERE payload_path <> $except {expiryFilter}
            ORDER BY {orderBy}
            LIMIT $page_size OFFSET $offset
            """);
        command.Parameters.AddWithValue("$except", except);
        command.Parameters.AddWithValue("$page_size", pageSize);
        command.Parameters.AddWithValue("$offset", offset);
        if (expiredAt.HasValue)
            command.Parameters.AddWithValue("$expires", expiredAt.Value.UtcDateTime.Ticks);

        using var reader = command.ExecuteReader();
        var results = new List<CacheFileEntry>(pageSize);
        while (reader.Read())
        {
            results.Add(new(reader.GetString(0),
                            reader.GetString(1),
                            reader.GetInt64(2),
                            reader.GetInt64(3),
                            reader.GetInt64(4),
                            reader.IsDBNull(5) ? null : reader.GetInt64(5)));
        }

        return results;
    }

    /// <summary>Coalesces a successful payload read into the pending batch for its publication.</summary>
    internal void RecordHit(CacheFileEntry entry)
    {
        var timestamp = DateTime.UtcNow.Ticks;
        // Keep the latest observation if the system clock moves backward between two hits.
        if (pendingHits.TryGetValue(entry.Path, out var previous) && previous.EntryVersion == entry.EntryVersion)
        {
            pendingHits[entry.Path] = (entry.EntryVersion, Math.Max(timestamp, previous.Timestamp));
        }
        else
        {
            if (pendingHits.Count >= MaxPendingHitCount)
                FlushHits();

            pendingHits[entry.Path] = (entry.EntryVersion, timestamp);
        }

        // A one-shot flush avoids waking up for idle directories.
        hitTimer ??= new(OnHitTimer, null, HitFlushInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Persists the pending batch, applying hits only to the publication that was read.</summary>
    /// <remarks>Trim calls this before querying LRU order so the SQL index includes recently buffered hits.</remarks>
    internal void FlushHits()
    {
        if (pendingHits.Count == 0)
            return;

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = CreateCommand(connection, """
            UPDATE cache_entries SET last_used_at_utc = $timestamp
            WHERE payload_path = $path AND entry_version = $version
              AND (last_used_at_utc IS NULL OR last_used_at_utc < $timestamp)
            """, transaction);
        var path = command.Parameters.Add("$path", SqliteType.Text);
        var version = command.Parameters.Add("$version", SqliteType.Text);
        var timestamp = command.Parameters.Add("$timestamp", SqliteType.Integer);
        foreach (var hit in pendingHits)
        {
            path.Value = hit.Key;
            version.Value = hit.Value.EntryVersion;
            timestamp.Value = hit.Value.Timestamp;
            command.ExecuteNonQuery();
        }

        // Keep the batch for another attempt if the transaction fails.
        transaction.Commit();
        pendingHits.Clear();
    }

    /// <summary>Warns when the marker identifies another live process using the directory.</summary>
    /// <remarks>The marker provides best-effort diagnostics. It grants no exclusive ownership.</remarks>
    internal void CheckForOtherProcess()
    {
        // This marker is diagnostic only. PID visibility varies by host and container.
        var marker = Path.Combine(directory, OwnerFileName);
        try
        {
            using var current = Process.GetCurrentProcess();
            // Start time distinguishes a live owner from an unrelated process reusing its PID.
            var currentStamp = FormattableString.Invariant($"{current.Id}:{current.StartTime.ToUniversalTime().Ticks}");
            if (File.Exists(marker))
            {
                var owner = File.ReadAllText(marker).Split(':');
                if (owner.Length == 2 && int.TryParse(owner[0], out var pid)
                                      && long.TryParse(owner[1], out var started)
                                      && pid != current.Id && IsRunning(pid, started))
                {
                    logger.LogWarning("Another process ({ProcessId}) is using filesystem cache directory {CacheDirectory}. "
                                      + "Capacity coordination supports one process per directory.", pid, directory);
                    return;
                }
            }

            File.WriteAllText(marker, currentStamp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                       Win32Exception or InvalidOperationException or NotSupportedException)
        {
            logger.LogDebug(exception, "Could not check process ownership of cache directory {CacheDirectory}.", directory);
        }
    }

    /// <summary>Measures the database and SQLite sidecar files separately from published entry bytes.</summary>
    internal long MeasureCatalogSizeBytes()
    {
        string[] suffixes = ["", "-journal", "-wal", "-shm"];
        return suffixes.Sum(suffix =>
        {
            var file = new FileInfo(databasePath + suffix);
            return file.Exists ? file.Length : 0;
        });
    }

    private void OnHitTimer(object? state)
    {
        // The timer holds no method-key lock. Take the directory gate before touching the pending hit batch.
        lock (directoryState.Gate)
        {
            hitTimer?.Dispose();
            hitTimer = null;
            try
            {
                FlushHits();
            }
            catch (Exception exception)
            {
                // Timer failures cannot be reported to a caller. Keep the batch for the next flush attempt.
                logger.LogWarning(exception, "Could not persist cache hits for {CacheDirectory}.", directory);
            }
        }
    }

    private SqliteConnection Open(bool writableCatalogExists = true)
    {
        // Short-lived, unpooled connections let a consumer remove an idle cache directory.
        // Only reconciliation may create a missing database so ordinary writes cannot open an empty index.
        var arguments = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = writableCatalogExists ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(arguments.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static long GetEntrySizeBytes(SqliteConnection connection, string path)
    {
        using var command = CreateCommand(connection, """
            SELECT payload_size_bytes + metadata_size_bytes FROM cache_entries WHERE payload_path = $path
            """);
        command.Parameters.AddWithValue("$path", path);
        return command.ExecuteScalar() is long sizeBytes ? sizeBytes : 0;
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static void SetEntryParameters(SqliteCommand command, CacheFileEntry entry)
    {
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$version", entry.EntryVersion);
        command.Parameters.AddWithValue("$payload_size_bytes", entry.PayloadSizeBytes);
        command.Parameters.AddWithValue("$metadata_size_bytes", entry.MetadataSizeBytes);
        command.Parameters.AddWithValue("$published", entry.PublishedAtUtc);
        command.Parameters.AddWithValue("$expires", (object?)entry.ExpiresAtUtc ?? DBNull.Value);
    }

    private static bool IsRunning(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime().Ticks == started;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>A snapshot of the files and catalog fields for one publication, without reading the payload.</summary>
internal sealed class CacheFileEntry(string path,
                                     string entryVersion,
                                     long payloadSizeBytes,
                                     long metadataSizeBytes,
                                     long publishedAtUtc,
                                     long? expiresAtUtc)
{
    /// <summary>The payload path relative to the cache directory, normalized for filesystem casing.</summary>
    internal string Path { get; } = path;

    /// <summary>The metadata token and file stamps used to match hits and eviction candidates to this version.</summary>
    internal string EntryVersion { get; } = entryVersion;
    internal long PayloadSizeBytes { get; } = payloadSizeBytes;
    internal long MetadataSizeBytes { get; } = metadataSizeBytes;

    /// <summary>The payload write time in UTC ticks, also used as recency before the first hit.</summary>
    internal long PublishedAtUtc { get; } = publishedAtUtc;

    /// <summary>A stable expiry deadline in UTC ticks, or null when the policy must be evaluated directly.</summary>
    internal long? ExpiresAtUtc { get; } = expiresAtUtc;
    internal long TotalSizeBytes => PayloadSizeBytes + MetadataSizeBytes;
}
