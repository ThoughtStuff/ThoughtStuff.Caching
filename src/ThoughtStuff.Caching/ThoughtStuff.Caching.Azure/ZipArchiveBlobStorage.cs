// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ThoughtStuff.Caching.Azure;

public class ZipArchiveBlobStorage : IZipArchiveBlobStorage
{
    private readonly IBlobStorageService blobStorageService;

    public ZipArchiveBlobStorage(IBlobStorageService blobStorageService)
    {
        this.blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
    }

    /// <inheritdoc/>
    public async Task<List<Uri>> UploadZipFiles(Stream zipFileStream, string blobPathPrefix)
    {
        if (zipFileStream is null)
            throw new ArgumentNullException(nameof(zipFileStream));
        if (string.IsNullOrEmpty(blobPathPrefix))
            throw new ArgumentException($"'{nameof(blobPathPrefix)}' cannot be null or empty.", nameof(blobPathPrefix));
        // Add virtual directory path separator if missing
        if (!blobPathPrefix.EndsWith("/"))
            blobPathPrefix += "/";
        using var zipArchive = new ZipArchive(zipFileStream);
        var entries = zipArchive.Entries;
        var blobUrls = new List<Uri>(entries.Count);
        foreach (var entry in entries)
        {
            using var stream = entry.Open();
            if (entry.Name != entry.FullName)
                throw new NotImplementedException($"Extracting non-flat zip files is not implemented.");
            var name = blobPathPrefix + entry.Name;
            var url = await blobStorageService.UploadStream(name, stream);
            blobUrls.Add(url);
        }
        return blobUrls;
    }
}
