#if NETSTANDARD2_0
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.
#endif

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ThoughtStuff.Caching.Core;
using static System.Environment;
using static ThoughtStuff.Caching.CachingInternal;

namespace ThoughtStuff.Caching.FileSystem;

public class LocalFileCache : ITextCache, IManagedCache
{
    // Bound synchronous eviction work per write. A later write can continue trimming an oversized cache.
    private const int TrimBatchSize = 32;
    private static readonly Random Random = new();
    private readonly IObjectFileSerializer objectFileSerializer;
    private readonly ICacheExpirationService cacheExpirationService;
    private readonly IOptions<LocalFileCacheOptions> options;
    private readonly ILogger<LocalFileCache> logger;
    private readonly Lazy<LocalFileCacheDirectory> directoryState;
    private string? baseDirectory;

    /// <summary>Creates a filesystem cache with the supplied serializer, expiration policy, and options.</summary>
    public LocalFileCache(IObjectFileSerializer objectFileSerializer,
                          ICacheExpirationService cacheExpirationService,
                          IOptions<LocalFileCacheOptions> options)
        : this(objectFileSerializer, cacheExpirationService, options, NullLogger<LocalFileCache>.Instance)
    {
    }

    /// <summary>Creates a filesystem cache with logging for capacity management and access tracking.</summary>
    public LocalFileCache(IObjectFileSerializer objectFileSerializer,
                          ICacheExpirationService cacheExpirationService,
                          IOptions<LocalFileCacheOptions> options,
                          ILogger<LocalFileCache>? logger = null)
    {
        this.objectFileSerializer = objectFileSerializer ?? throw new ArgumentNullException(nameof(objectFileSerializer));
        this.cacheExpirationService = cacheExpirationService ?? throw new ArgumentNullException(nameof(cacheExpirationService));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? NullLogger<LocalFileCache>.Instance;
        directoryState = new(() => LocalFileCacheDirectory.Get(BaseDirectory));
    }

    /// <summary>Gets the filesystem directory that holds the cache entries.</summary>
    public string BaseDirectory
    {
        // A Lazy-accessor for BaseDirectory to avoid possible exceptions in the constructor
        get
        {
            if (baseDirectory is not null)
                return baseDirectory;
            if ((options?.Value?.BaseDirectory).IsEmpty())
                throw new Exception($"Missing required configuration {LocalFileCacheOptions.Name}.{nameof(LocalFileCacheOptions.BaseDirectory)}.");
            var configuredDirectory = options.Value.BaseDirectory;
            Directory.CreateDirectory(configuredDirectory);
            return baseDirectory = configuredDirectory;
        }
    }

    /// <summary>
    /// Find a candidate directory in the local system environment that is appropriate to use for app file cache
    /// <para/>
    /// Looks for environment variables related to UserProfiles, AppData, and Temp.
    /// <para/>
    /// DOES NOT CHECK IF DIRECTORY EXISTS OR HAS CACHE ENTRIES.
    /// </summary>
    public static string FindLocalCacheBaseDirectory()
    {
        string?[] candidates =
        [
            GetFolderPath(SpecialFolder.UserProfile),
            GetFolderPath(SpecialFolder.ApplicationData),
            GetEnvironmentVariable("APPDATA"),
            GetEnvironmentVariable("TEMP"),
            GetEnvironmentVariable("TMP")
        ];
        // GetFolderPath will return an empty string if the special folder does not exist.
        // So null coalescing doesn't work. The following check must consider null or empty string case.
        foreach (var candidate in candidates)
        {
            if (candidate.IsNotEmpty())
            {
                var baseDirectory = Path.Combine(candidate, "ThoughtStuff", nameof(LocalFileCache));
                return baseDirectory;
            }
        }
        throw new InvalidOperationException($"Cannot get folder {SpecialFolder.UserProfile} or {SpecialFolder.ApplicationData} to use for local file caching.");
    }

    // TODO: Handle path length limitations
    //       https://stackoverflow.com/questions/3406494/what-is-the-maximum-amount-of-characters-or-length-for-a-directory

    /// <inheritdoc/>
    public string GetLocation(string key) => GetFilePath(key);

    /// <inheritdoc/>
    /// <remarks>Expiration can remove files. Capacity coordination supports one process per directory.</remarks>
    public bool Contains(string key)
    {
        var valuePath = GetFilePath(key);
        // Take the directory gate before checking expiry because that check can delete files and update the catalog.
        lock (directoryState.Value.Gate)
            return Contains(valuePath, GetCatalog(), out _);
    }

    private bool Contains(string valuePath, LocalFileCacheCatalog? catalog, out LocalFileCacheMetadata? metadata)
    {
        var metadataPath = GetMetadataPath(valuePath);
        if (!File.Exists(valuePath) && !File.Exists(metadataPath))
        {
            metadata = null;
            return false;
        }

        var updatedTime = new DateTimeOffset(File.GetLastWriteTimeUtc(valuePath));
        var isExpired = IsExpired(metadataPath, updatedTime, out metadata);
        if (!isExpired)
            return File.Exists(valuePath);
        RemoveEntryFiles(valuePath, catalog);
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>Expiration and access tracking assume one process per directory when capacity management is enabled.</remarks>
    public string? GetString(string key)
    {
        var valuePath = GetFilePath(key);
        // Hold the directory gate across the read so the hit is credited to the version that was read.
        lock (directoryState.Value.Gate)
        {
            var catalog = GetCatalog();
            if (!Contains(valuePath, catalog, out var metadata))
                return null;

            try
            {
                var entry = catalog is null ? null : DescribeEntry(valuePath, metadata);
                var value = WithRetry(retryMissingFiles: false).Execute(() => File.ReadAllText(valuePath));
                if (entry is not null)
                    catalog!.RecordHit(entry);
                return value;
            }
            catch (IOException exception) when (IsMissingFile(exception))
            {
                return null;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Capacity management may skip a write and supports one process per directory.</remarks>
    public void SetString(string key, string value, DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration.HasValue)
            throw new NotSupportedException($"{nameof(LocalFileCache)} does not support {nameof(DistributedCacheEntryOptions.SlidingExpiration)}.");
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ProhibitDefaultValue(key, value);

        var valuePath = GetFilePath(key);
        // The value is ready, so the directory gate covers storage admission, eviction, and publication.
        lock (directoryState.Value.Gate)
        {
            var catalog = GetCatalog();
            var metadata = new LocalFileCacheMetadata { CacheEntryOptions = options };
            if (catalog is null)
            {
                WritePayload(valuePath, value);
                WithRetry().Execute(() => objectFileSerializer.SerializeToFile(GetMetadataPath(valuePath), metadata));
                return;
            }

            var directory = directoryState.Value;
            directory.SoftSizeLimitBytes = this.options.Value.SoftSizeLimitBytes ?? directory.SoftSizeLimitBytes;
            WriteWithinLimit(valuePath, value, metadata, catalog, directory.SoftSizeLimitBytes);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Capacity accounting supports one process per directory.</remarks>
    public void Remove(string key) => RemoveEntryFiles(GetFilePath(key));

    /// <inheritdoc/>
    /// <remarks>Capacity accounting for wildcard removal supports one process per directory.</remarks>
    public ICacheManager GetCacheManager() => new LocalFileCacheManager(this);

    internal bool IsExpired(string metadataPath, DateTimeOffset updatedTime) =>
        IsExpired(metadataPath, updatedTime, out _);

    private bool IsExpired(string metadataPath, DateTimeOffset updatedTime, out LocalFileCacheMetadata? metadata)
    {
        metadata = null;
        try
        {
            metadata = ReadMetadata(metadataPath);
            // Missing metadata uses the default cache policy.
            return cacheExpirationService.IsExpired(metadata?.CacheEntryOptions, updatedTime);
        }
        catch (IOException exception) when (IsMissingFile(exception))
        {
            // The companion disappeared after the existence check. Let the payload check decide the result.
            return false;
        }
    }

    private static string GetMetadataPath(string valuePath) => Path.ChangeExtension(valuePath, ".meta");

    /// <summary>Removes an entry by its on-disk payload path and synchronizes the directory's catalog.</summary>
    /// <remarks>Wildcard deletion passes enumerated paths here so the original filename is preserved.</remarks>
    internal void RemoveEntryFiles(string valuePath)
    {
        // Take the directory gate before deleting either file so a writer cannot replace it mid-removal.
        lock (directoryState.Value.Gate)
            RemoveEntryFiles(valuePath, GetCatalog());
    }

    private void RemoveEntryFiles(string valuePath, LocalFileCacheCatalog? catalog)
    {
        try
        {
            // Keep the catalog row until both files are gone. If any step fails, the next
            // inventory repairs accounting from what is actually on disk.
            Delete(valuePath);
            Delete(GetMetadataPath(valuePath));
            catalog?.Remove(GetCatalogPath(valuePath));
        }
        catch
        {
            if (catalog is not null)
                catalog.NeedsReconciliation = true;
            throw;
        }
    }

    private LocalFileCacheMetadata? ReadMetadata(string path) => File.Exists(path)
        ? WithRetry(retryMissingFiles: false).Execute(() => objectFileSerializer.DeserializeFromFile<LocalFileCacheMetadata>(path))
        : null;

    /// <summary>Gets the directory's catalog and reconciles it if it has no trustworthy inventory.</summary>
    /// <remarks>Requires the directory gate. A configured instance enables the catalog for the whole directory.</remarks>
    private LocalFileCacheCatalog? GetCatalog()
    {
        var directory = directoryState.Value;
        var limit = options.Value.SoftSizeLimitBytes;
        if (limit is < 0)
            throw new ArgumentOutOfRangeException(nameof(LocalFileCacheOptions.SoftSizeLimitBytes));

        var catalog = directory.Catalog;
        if (catalog is null)
        {
            if (limit is null)
                return null;

            directory.DetectPathCaseSensitivity();
            catalog = new(directory, logger);
            catalog.CheckForOtherProcess();
            directory.SoftSizeLimitBytes = limit.Value;
            directory.Catalog = catalog;
        }

        if (catalog.NeedsReconciliation || !catalog.Exists)
        {
            catalog.Reconcile(EnumerateEntries(directory.DirectoryPath));
            LogInventory(directory.DirectoryPath, catalog);
        }

        return catalog;
    }

    /// <summary>Attempts admission and publishes the payload and metadata before updating byte accounting.</summary>
    /// <remarks>Requires the directory gate through sizing, eviction, publication, and indexing.</remarks>
    private void WriteWithinLimit(string valuePath,
                                  string value,
                                  LocalFileCacheMetadata metadata,
                                  LocalFileCacheCatalog catalog,
                                  long limit)
    {
        var payloadSizeBytes = Encoding.UTF8.GetByteCount(value);
        if (payloadSizeBytes > limit)
            return;

        var metadataPath = GetMetadataPath(valuePath);
        if (IsLink(new FileInfo(valuePath)) || IsLink(new FileInfo(metadataPath)))
            throw new IOException($"Cannot publish cache entry '{valuePath}' through a filesystem link.");

        // Measure the serializer's actual output before deleting any entries to make room.
        var staging = Path.Combine(directoryState.Value.DirectoryPath, LocalFileCacheCatalog.StagingDirectoryName);
        Directory.CreateDirectory(staging);
        metadata.EntryVersion = Guid.NewGuid().ToString("N");
        var stagedMetadata = Path.Combine(staging, metadata.EntryVersion + ".meta");
        try
        {
            WithRetry().Execute(() => objectFileSerializer.SerializeToFile(stagedMetadata, metadata));
            var incomingSizeBytes = payloadSizeBytes + new FileInfo(stagedMetadata).Length;
            if (incomingSizeBytes > limit)
                return;

            var path = GetCatalogPath(valuePath);
            // Replacing the incoming path reclaims its previous bytes without evicting that entry.
            var maxExistingSizeBytes = limit - incomingSizeBytes;
            maxExistingSizeBytes += Math.Min(catalog.GetEntrySizeBytes(path), long.MaxValue - maxExistingSizeBytes);
            if (!TryMakeRoom(catalog, path, maxExistingSizeBytes))
                return;

            // File publication and its catalog update are separate. A failed update leaves disk
            // authoritative and causes the next operation to reconcile the catalog.
            try
            {
                WritePayload(valuePath, value);
                WithRetry().Execute(() => File.Copy(stagedMetadata, metadataPath, overwrite: true));
                var entry = DescribeEntry(valuePath, metadata) ?? throw new IOException($"Cannot account for cache entry '{valuePath}'.");
                catalog.Store(entry);
            }
            catch
            {
                catalog.NeedsReconciliation = true;
                throw;
            }
        }
        finally
        {
            try
            {
                File.Delete(stagedMetadata);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not remove staged cache metadata at {MetadataPath}.", stagedMetadata);
            }
        }
    }

    /// <summary>Tries expiry and then LRU under the same removal budget so admission stays best effort.</summary>
    private bool TryMakeRoom(LocalFileCacheCatalog catalog, string incomingPath, long maxExistingSizeBytes)
    {
        if (catalog.TotalSizeBytes <= maxExistingSizeBytes)
            return true;

        // Flush pending hits into the indexed order before selecting candidates.
        catalog.FlushHits();
        var removalsRemaining = TrimBatchSize;
        if (Trim(catalog, incomingPath, maxExistingSizeBytes, DateTimeOffset.UtcNow, ref removalsRemaining))
            return true;

        return !catalog.NeedsReconciliation && Trim(catalog, incomingPath, maxExistingSizeBytes, null, ref removalsRemaining);
    }

    private bool Trim(LocalFileCacheCatalog catalog,
                      string incomingPath,
                      long maxExistingSizeBytes,
                      DateTimeOffset? expiredAt,
                      ref int removalsRemaining)
    {
        long offset = 0;
        while (catalog.TotalSizeBytes > maxExistingSizeBytes && removalsRemaining > 0)
        {
            var candidates = catalog.ReadCandidates(incomingPath, expiredAt, offset, TrimBatchSize);
            if (candidates.Count == 0)
                break;

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(directoryState.Value.DirectoryPath, candidate.Path);
                if (!TryInspectEntry(path, out var entry, out var metadata) ||
                    entry.EntryVersion != candidate.EntryVersion)
                {
                    // A different publication is never deleted using a stale selection.
                    catalog.NeedsReconciliation = true;
                    return false;
                }

                // An indexed deadline is only a hint. A custom or changed policy can keep an
                // entry live, so use the same expiration policy as a normal read.
                if (expiredAt is not null &&
                    !cacheExpirationService.IsExpired(metadata?.CacheEntryOptions,
                                                      new DateTimeOffset(entry.PublishedAtUtc, TimeSpan.Zero)))
                {
                    // Only surviving rows contribute to the next page's offset.
                    ++offset;
                    continue;
                }

                RemoveEntryFiles(path, catalog);
                --removalsRemaining;
                if (catalog.TotalSizeBytes <= maxExistingSizeBytes || removalsRemaining == 0)
                    break;
            }
        }

        return catalog.TotalSizeBytes <= maxExistingSizeBytes;
    }

    private IEnumerable<CacheFileEntry> EnumerateEntries(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.txt"))
        {
            if (TryInspectEntry(path, out var entry, out _))
                yield return entry;
        }
    }

    /// <summary>Reads file stamps and expiry metadata for inventory or eviction without loading the payload.</summary>
    private bool TryInspectEntry(string path,
                                 out CacheFileEntry entry,
                                 out LocalFileCacheMetadata? metadata)
    {
        metadata = null;
        entry = null!;
        var payload = new FileInfo(path);
        var companion = new FileInfo(GetMetadataPath(path));
        // Reject links so inventory and eviction stay within the cache directory.
        if (!IsRegularFile(payload) || (companion.Exists && !IsRegularFile(companion)))
            return false;

        try
        {
            metadata = ReadMetadata(companion.FullName);
        }
        catch (JsonException)
        {
            // A bad companion must not prevent other legacy entries from being inventoried.
            // Direct reads retain the existing metadata error behavior.
        }
        catch (IOException exception) when (IsMissingFile(exception))
        {
            return false;
        }

        entry = DescribeEntry(path, metadata)!;
        return entry is not null;
    }

    private CacheFileEntry? DescribeEntry(string path, LocalFileCacheMetadata? metadata)
    {
        var payload = new FileInfo(path);
        var companion = new FileInfo(GetMetadataPath(path));
        if (!IsRegularFile(payload) || (companion.Exists && !IsRegularFile(companion)))
            return null;

        var metadataSizeBytes = companion.Exists ? companion.Length : 0;
        var metadataTime = companion.Exists ? companion.LastWriteTimeUtc.Ticks : 0;
        var payloadTime = payload.LastWriteTimeUtc;
        // Legacy entries have no persisted generation. File stamps also catch changes made by
        // a caller that updated an existing payload outside this coordinator.
        var entryVersion = FormattableString.Invariant(
            $"{metadata?.EntryVersion ?? "legacy"}:{payloadTime.Ticks}:{payload.Length}:{metadataTime}:{metadataSizeBytes}");
        var expiration = cacheExpirationService.GetType() == typeof(CacheExpirationService)
            ? ((CacheExpirationService)cacheExpirationService).GetExpirationHint(metadata?.CacheEntryOptions, payloadTime)
            : null;
        return new(GetCatalogPath(path),
                   entryVersion,
                   payload.Length,
                   metadataSizeBytes,
                   payloadTime.Ticks,
                   expiration?.UtcDateTime.Ticks);
    }

    private void LogInventory(string directory, LocalFileCacheCatalog catalog)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        var staging = new DirectoryInfo(Path.Combine(directory, LocalFileCacheCatalog.StagingDirectoryName));
        var stagedSizeBytes = staging.Exists ? staging.EnumerateFiles().Where(IsRegularFile).Sum(file => file.Length) : 0;
        var orphanSizeBytes = new DirectoryInfo(directory).EnumerateFiles("*.meta")
                                                     .Where(file => IsRegularFile(file) &&
                                                                    !File.Exists(Path.ChangeExtension(file.FullName, ".txt")))
                                                     .Sum(file => file.Length);
        logger.LogDebug("Cache inventory at {CacheDirectory} has {EntryBytes} published bytes, {StagingBytes} staging bytes, "
                        + "{OrphanBytes} orphaned metadata bytes and {CatalogBytes} SQLite bytes.",
                        directory, catalog.TotalSizeBytes, stagedSizeBytes, orphanSizeBytes, catalog.MeasureCatalogSizeBytes());
    }

    private static bool IsRegularFile(FileInfo file) => file.Exists && !IsLink(file);

    private static bool IsLink(FileInfo file) => file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0;

    private string GetCatalogPath(string path)
    {
        var name = Path.GetFileName(path);
        return directoryState.Value.PathsIgnoreCase ? name.ToUpperInvariant() : name;
    }

    private static void WritePayload(string path, string value) =>
        WithRetry().Execute(() => File.WriteAllText(path, value));

    internal string GetFilePath(string key, string extension = "txt")
    {
        var fileName = GetFileName(key, extension);
        return Path.Combine(BaseDirectory, fileName);
    }

    internal static string GetFileName(string key, string extension = "txt", bool keepWildcards = false)
    {
        if (key.IsEmpty())
            throw new ArgumentException("Cache key cannot be null or whitespace");
        if (extension.IsEmpty() || extension == ".")
            throw new ArgumentException("File extension cannot be null or whitespace");
        // TODO: Ensure valid extension
        var fileName = key.Trim();
        // For some consistency across OSes, consider some characters invalid everywhere
        var invalidChars = new HashSet<char> { '\\', ':' };
        invalidChars.UnionWith(Path.GetInvalidFileNameChars());
        if (keepWildcards)
        {
            // This code path helps when searching for entries that match a search pattern
            invalidChars.Remove('*');
            invalidChars.Remove('?');
        }
        // Replacing prohibited strings
        const char x = '_';
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, x);
        }
        // Avoid double dot
        fileName = fileName.Replace("..", $"{x}{x}");
        if (fileName.EndsWith("."))
            fileName = fileName.Trim('.') + x;
        // Avoid reserved names by appending a character
        string[] reservedNames =
        [
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "GLOBALROOT"
        ];
        if (reservedNames.Contains(fileName, StringComparer.InvariantCultureIgnoreCase))
            fileName += x;
        // TODO: Handle max path limit https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#maximum-path-length-limitation
        return $"{fileName}.{extension}";
    }

    private static void Delete(string valuePath)
    {
        WithRetry().Execute(() =>
            File.Delete(valuePath));
    }

    private static bool IsMissingFile(IOException exception) =>
        exception is FileNotFoundException or DirectoryNotFoundException;

    private static RetryPolicy WithRetry(bool retryMissingFiles = true)
    {
        // https://github.com/App-vNext/Polly/wiki/Retry
        return Policy
            .Handle<UnauthorizedAccessException>()
            .Or<IOException>(exception => retryMissingFiles || !IsMissingFile(exception))
            .WaitAndRetry(
              [
                TimeSpan.FromMilliseconds(Random.Next(250)),
                TimeSpan.FromMilliseconds(Random.Next(500)),
                TimeSpan.FromMilliseconds(Random.Next(1000)),
                TimeSpan.FromMilliseconds(Random.Next(2000)),
                TimeSpan.FromMilliseconds(Random.Next(4000)),
                TimeSpan.FromMilliseconds(Random.Next(8000)),
                TimeSpan.FromMilliseconds(Random.Next(16000)),
                TimeSpan.FromMilliseconds(Random.Next(32000)),
              ]);
    }
}
