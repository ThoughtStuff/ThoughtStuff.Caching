// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.FileSystem;

public class LocalFileCacheOptions
{
    public const string Name = nameof(LocalFileCacheOptions);
    public string BaseDirectory { get; set; }
}
