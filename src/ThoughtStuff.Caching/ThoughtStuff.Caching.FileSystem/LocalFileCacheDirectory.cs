namespace ThoughtStuff.Caching.FileSystem;

/// <summary>Holds the lock and catalog used by all <see cref="LocalFileCache"/> objects for one directory.</summary>
internal sealed class LocalFileCacheDirectory(string path)
{
    /// <summary>Maps each normalized directory path to its <see cref="LocalFileCacheDirectory"/>.</summary>
    /// <remarks>
    /// <c>AddLocalFileTextCache</c> registers <see cref="ITextCache"/> as transient, so each
    /// resolution creates a new <see cref="LocalFileCache"/> object. Those objects can point at
    /// the same files on disk. The static map gives them one directory lock and one SQLite
    /// catalog for byte accounting and buffered reads, even across service providers. Keys
    /// use exact casing because two case variants can name separate directories.
    /// </remarks>
    private static readonly Dictionary<string, LocalFileCacheDirectory> Directories = new(StringComparer.Ordinal);

    /// <summary>
    /// Serializes entry-file reads and mutations with catalog transactions, byte accounting,
    /// and access-buffer updates for all cache instances and the flush timer in this process.
    /// </summary>
    /// <remarks>
    /// If a method-cache per-key lock is needed, take it before this gate. Take this gate
    /// before starting a catalog transaction. While holding it, keep work synchronous and
    /// avoid invoking cached methods, acquiring another cache lock, or waiting for work
    /// that may acquire this gate. Custom serializers and expiry policies run under this gate
    /// and must follow the same rules. Uncapped instances use the gate because they can share
    /// files with a capped instance. Future multi-process coordination must cover these same operations.
    /// The .NET Standard targets lack <c>System.Threading.Lock</c>, so this gate uses a monitor.
    /// </remarks>
    internal object Gate { get; } = new();
    internal string DirectoryPath { get; } = path;

    /// <summary>Whether the filesystem treats entry paths that differ only in casing as the same path.</summary>
    internal bool PathsIgnoreCase { get; private set; }

    /// <summary>The directory's active limit, also used by instances without a configured limit of their own.</summary>
    internal long SoftSizeLimitBytes { get; set; }

    /// <summary>The shared catalog, initialized on the first capacity-managed operation in the directory.</summary>
    internal LocalFileCacheCatalog? Catalog { get; set; }

    /// <summary>Gets or creates the coordinator for an existing directory, resolving case-only aliases.</summary>
    /// <remarks>Paths are normalized to absolute form. Other aliases, such as symbolic links, are not resolved.</remarks>
    internal static LocalFileCacheDirectory Get(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        if (fullPath != Path.GetPathRoot(fullPath))
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Hold the map lock through lookup and insertion so two spellings cannot each create
        // their own coordinator. This lock never waits for a directory gate.
        lock (Directories)
        {
            if (Directories.TryGetValue(fullPath, out var existing))
                return existing;

            // Only inspect case variants. String comparison alone cannot establish filesystem identity.
            var candidates = Directories.Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Key, fullPath))
                                        .Select(pair => pair.Value)
                                        .ToArray();
            var alias = candidates.Length == 0 ? null : FindAlias(fullPath, candidates);
            return Directories[fullPath] = alias ?? new(fullPath);
        }
    }

    /// <summary>Finds a coordinator whose directory can see a file created through <paramref name="path"/>.</summary>
    internal static LocalFileCacheDirectory? FindAlias(string path, IEnumerable<LocalFileCacheDirectory> candidates) =>
        WithProbeFile(path, name => candidates.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(candidate.DirectoryPath, name))));

    /// <summary>Checks whether this directory lets callers open the same file under a different case.</summary>
    /// <remarks>Use the actual cache directory because filesystem case rules can vary by volume or directory.</remarks>
    internal void DetectPathCaseSensitivity() =>
        PathsIgnoreCase = WithProbeFile(DirectoryPath, name =>
            File.Exists(Path.Combine(DirectoryPath, name.ToUpperInvariant())));

    /// <summary>Creates a unique temporary file and passes its filename to a filesystem identity or casing check.</summary>
    /// <remarks>
    /// The unpredictable name makes an existing file unlikely to satisfy the check. CreateNew
    /// keeps the probe from overwriting a file, and cleanup runs even if the check throws.
    /// </remarks>
    internal static T WithProbeFile<T>(string path, Func<string, T> inspect)
    {
        var probeName = $".thoughtstuff-directory-{Guid.NewGuid():N}.probe";
        var probePath = Path.Combine(path, probeName);
        using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write))
        {
        }

        try
        {
            return inspect(probeName);
        }
        finally
        {
            File.Delete(probePath);
        }
    }
}
