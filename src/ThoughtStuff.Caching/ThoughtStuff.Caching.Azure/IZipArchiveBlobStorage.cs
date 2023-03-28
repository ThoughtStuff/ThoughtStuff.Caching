// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Azure;

public interface IZipArchiveBlobStorage
{
    /// <summary>
    /// Extract files from the zip archive stream <paramref name="zipFileStream"/>
    /// and upload each file to Blob storage in the virtual "directory"
    /// specified by <paramref name="blobPathPrefix"/>.
    /// <para/>
    /// The <paramref name="blobPathPrefix"/> is always treated as a Directory.
    /// So if it lacks a trailing slash, one will be added.
    /// So all of the zip files will be in a virtual directory.
    /// </summary>
    /// <returns>The URLs of the uploaded files</returns>
    Task<List<Uri>> UploadZipFiles(Stream zipFileStream, string blobPathPrefix);
}