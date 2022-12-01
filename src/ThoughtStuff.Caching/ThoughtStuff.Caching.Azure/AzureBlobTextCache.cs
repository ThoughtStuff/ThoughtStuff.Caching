// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using ThoughtStuff.Core;

namespace ThoughtStuff.Caching.Azure;

public class AzureBlobTextCache : ITextCache, IManagedCache
{
    private readonly IBlobStorageService blobStorageService;
    private readonly ICacheExpirationService cacheExpirationService;

    public AzureBlobTextCache(IBlobStorageService blobStorageService, ICacheExpirationService cacheExpirationService)
    {
        this.blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        this.cacheExpirationService = cacheExpirationService ?? throw new ArgumentNullException(nameof(cacheExpirationService));
    }

    /// <inheritdoc/>
    public bool Contains(string key)
    {
        var blobName = KeyToBlobName(key);
        var exists = blobStorageService.ExistsBlocking(blobName);
        if (!exists)
            return false;
        var (modified, options) = blobStorageService.GetMetadataBlocking<DistributedCacheEntryOptions>(blobName);
        var isExpired = cacheExpirationService.IsExpired(options, modified);
        if (isExpired)
        {
            blobStorageService.DeleteBlocking(blobName);
            return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public string GetString(string key)
    {
        if (!Contains(key))
            return default;
        var blobName = KeyToBlobName(key);
        return blobStorageService.GetTextBlocking(blobName);
    }

    /// <inheritdoc/>
    public void SetString(string key, string value, DistributedCacheEntryOptions options)
    {
        var blobName = KeyToBlobName(key);
        blobStorageService.UploadTextAndMetadataBlocking(blobName, value, options);
    }

    /// <inheritdoc/>
    public string GetLocation(string key)
    {
        // TODO: Return the full URL of the blob
        //return blobStorageService.GetBlobUrl(blobName)
        return KeyToBlobName(key);
    }

    public ICacheManager GetCacheManager()
    {
        return new AzureBlobTextCacheManager(blobStorageService);
    }

    /// <summary>
    /// Converts a cache key to a valid blob name.
    /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#blob-names">
    /// Blob Name Rules
    /// </see>
    /// </summary>
    internal static string KeyToBlobName(string key, bool keepWildcards = false)
    {
        if (key.IsEmpty())
            throw new ArgumentException($"A cache key may not be null or empty", nameof(key));
        var name = key.Trim();
        // Backslashes will get changed to front slashes by blob storage
        name = name.Replace('\\', '/');

        // TODO: Replace other special characters
        var invalidChars = new List<char> { '*', '?' };
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
            name = name.Replace(c, x);
        }

        // Special treatment for method-based keys
        // (Which would all be simpler if the method-based keys used slashes as delimeters...)
        // Want the method name to be the virtual 'top level directory'
        // And want parameters to be grouped into virtual directories
        if (name.Contains("("))
        {
            var trailingWildcard = keepWildcards && (name.EndsWith("*") || name.EndsWith("?"));
            if (trailingWildcard)
            {
                // Special case for searching Foo(? or Foo(* because it needs to match Foo_ or Foo/a
                if (name.EndsWith("(*") || name.EndsWith("(?"))
                    name = name.ReplaceFirst("(", "?");
                else
                    name = name.ReplaceFirst("(", "/");
            }
            else if (name.EndsWith(")"))
            {
                name = name
                    .Trim(')')
                    .ReplaceFirst("(", "/");
            }
            name = name
                .Replace(',', '/');
        }
        // Names not allowed to end in period
        if (name.EndsWith("."))
            name = name.Trim('.') + '_';
        // Prohibit double dot
        name = name.Replace("..", "__");
        // Replace consecutive slashes because it makes virtual folder browsing awkward
        name = name.Replace("//", "__");
        // If name ends in "/", replace it because it is awkward when browsing virtual directories
        // (Just dropping the slash would be nice, but makes it inconsistent with other caches)
        if (name.EndsWith("/"))
            name = name.TrimEnd('/') + '_';
        if (name.Length == 0)
            name = "_";
        // Local storage emulator only supports names of 256 chars. But Azure supports up to 1024 chars.
        if (name.Length >= 1024)
            // TODO: use a cryptographic hashing scheme for long names
            throw new NotImplementedException($"Blob cache names longer than 1024 characters are not yet supported.");
        return name;
    }
}
