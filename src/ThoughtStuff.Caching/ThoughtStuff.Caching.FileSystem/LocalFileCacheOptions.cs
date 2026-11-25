namespace ThoughtStuff.Caching.FileSystem;

public class LocalFileCacheOptions
{
    public const string Name = nameof(LocalFileCacheOptions);
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// Best-effort limit on published payload and metadata bytes. A non-null value enables
    /// capacity management for the directory. Share the directory only within one process.
    /// </summary>
    public long? SoftSizeLimitBytes { get; set; }
}
