// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThoughtStuff.Core;
using ThoughtStuff.Core.Abstractions;
using static System.Environment;
using static ThoughtStuff.Caching.CachingInternal;

namespace ThoughtStuff.Caching;

public class LocalFileCache : ITextCache, IManagedCache
{
    private static readonly Random Random = new();
    private readonly IObjectFileSerializer objectFileSerializer;
    private readonly ICacheExpirationService cacheExpirationService;
    private readonly IOptions<LocalFileCacheOptions> options;
    private string baseDirectory;

    public LocalFileCache(IObjectFileSerializer objectFileSerializer,
                          ICacheExpirationService cacheExpirationService,
                          IOptions<LocalFileCacheOptions> options)
    {
        this.objectFileSerializer = objectFileSerializer ?? throw new ArgumentNullException(nameof(objectFileSerializer));
        this.cacheExpirationService = cacheExpirationService ?? throw new ArgumentNullException(nameof(cacheExpirationService));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string BaseDirectory
    {
        // A Lazy-accessor for BaseDirectory to avoid possible exceptions in the constructor
        get
        {
            if (baseDirectory is not null)
                return baseDirectory;
            if ((options?.Value?.BaseDirectory).IsEmpty())
                throw new Exception($"Missing required configuration {LocalFileCacheOptions.Name}.{nameof(LocalFileCacheOptions.BaseDirectory)}.");
            baseDirectory = options.Value.BaseDirectory;
            Directory.CreateDirectory(baseDirectory);
            return baseDirectory;
        }
    }

    /// <summary>
    /// Find a candidate directory in the local system environment that is appropriate to use for app file cache
    /// <para/>
    /// Looks for environment vairables related to UserProfiles, AppData, and Temp.
    /// <para/>
    /// DOES NOT CHECK IF DIRECTORY EXISTS OR HAS CACHE ENTRIES.
    /// </summary>
    public static string FindLocalCacheBaseDirectory()
    {
        var candidates = new[]
        {
            GetFolderPath(SpecialFolder.UserProfile),
            GetFolderPath(SpecialFolder.ApplicationData),
            GetEnvironmentVariable("APPDATA"),
            GetEnvironmentVariable("TEMP"),
            GetEnvironmentVariable("TMP")
        };
        // GetFolderPath will return an empty string if the special folder does not exist.
        // So null coalescing doesn't work. The following check must consider null or empty string case.
        foreach (var candidate in candidates)
        {
            if (candidate.IsNotEmpty())
            {
                var baseDirectory = Path.Combine(candidate, "Unplugged.SecurityAnalysis", ".QuickCache");
                return baseDirectory;
            }
        }
        throw new InvalidOperationException($"Cannot get folder {SpecialFolder.UserProfile} or {SpecialFolder.ApplicationData} to use for local file caching.");
    }

    // TODO: Handle simultaneous access
    //       System.IO.IOException : The process cannot access the file 'C:\Users\JacobFoshee\Unplugged.SecurityAnalysis\.QuickCache\RequestDailyTimeSeriesAsync('AAPL',0,0).txt' because it is being used by another process.
    // TODO: Handle path length limitations
    //       https://stackoverflow.com/questions/3406494/what-is-the-maximum-amount-of-characters-or-length-for-a-directory

    /// <inheritdoc/>
    public string GetLocation(string key) => GetFilePath(key);

    /// <inheritdoc/>
    public bool Contains(string key)
    {
        var valuePath = GetFilePath(key);
        var updatedTime = new DateTimeOffset(File.GetLastWriteTimeUtc(valuePath));
        var metadataPath = GetMetadataPath(key);
        var isExpired = IsExpired(metadataPath, updatedTime);
        if (!isExpired)
            return File.Exists(valuePath);
        Delete(valuePath);
        Delete(metadataPath);
        return false;
    }

    /// <inheritdoc/>
    public string GetString(string key)
    {
        if (!Contains(key))
            return null;
        var valuePath = GetFilePath(key);
        return WithRetry().Execute(() =>
            File.ReadAllText(valuePath));
    }

    /// <inheritdoc/>
    public void SetString(string key, string value, DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration.HasValue)
            throw new NotSupportedException($"{nameof(LocalFileCache)} does not support {nameof(DistributedCacheEntryOptions.SlidingExpiration)}.");
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ProhibitDefaultValue(key, value);
        WithRetry().Execute(() =>
            File.WriteAllText(GetFilePath(key), value));
        var metadata = new LocalFileCacheMetadata
        {
            CacheEntryOptions = options
        };
        var metadataPath = GetMetadataPath(key);
        WithRetry().Execute(() =>
            objectFileSerializer.SerializeToFile(metadataPath, metadata));
    }

    /// <inheritdoc/>
    public ICacheManager GetCacheManager()
    {
        return new LocalFileCacheManager(this);
    }

    internal bool IsExpired(string metadataPath, DateTimeOffset updatedTime)
    {
        LocalFileCacheMetadata metadata = null;
        try
        {
            if (File.Exists(metadataPath))
            {
                metadata = WithRetry().Execute(() =>
                    objectFileSerializer.DeserializeFromFile<LocalFileCacheMetadata>(metadataPath));
            }
            // If the file does not exist, pass null cache entry options which will 
            // cause a default cache policy to be used.
            return cacheExpirationService.IsExpired(metadata?.CacheEntryOptions, updatedTime);
        }
        catch (FileNotFoundException)
        {
            // If the meta file existed, and then was not found while being deserialized
            // Assume another thread is updating it
            return false;
        }
    }

    private string GetMetadataPath(string key) =>
        GetFilePath(key, extension: "meta");

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
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "GLOBALROOT" }.ToList();
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

    private static RetryPolicy WithRetry()
    {
        // https://github.com/App-vNext/Polly/wiki/Retry
        return Policy
            .Handle<UnauthorizedAccessException>()
            .Or<IOException>()
            .WaitAndRetry(new[]
              {
                TimeSpan.FromMilliseconds(Random.Next(250)),
                TimeSpan.FromMilliseconds(Random.Next(500)),
                TimeSpan.FromMilliseconds(Random.Next(1000)),
                TimeSpan.FromMilliseconds(Random.Next(2000)),
                TimeSpan.FromMilliseconds(Random.Next(4000)),
                TimeSpan.FromMilliseconds(Random.Next(8000)),
                TimeSpan.FromMilliseconds(Random.Next(16000)),
                TimeSpan.FromMilliseconds(Random.Next(32000)),
              });
    }
}
