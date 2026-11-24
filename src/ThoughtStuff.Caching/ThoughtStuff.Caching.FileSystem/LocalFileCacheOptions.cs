namespace ThoughtStuff.Caching.FileSystem;

public class LocalFileCacheOptions
{
    public const string Name = nameof(LocalFileCacheOptions);
    public string? BaseDirectory { get; set; }
}
