// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ThoughtStuff.Caching.Azure;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a stream of data to Blob storage.
    /// </summary>
    /// <remarks>
    /// Leading and trailing path separators will be removed from the <paramref name="blobName"/>.
    /// </remarks>
    /// <returns>
    /// The URL of the uploaded blob
    /// </returns>
    Task<Uri> UploadStream(string blobName, Stream content);

    /// <summary>
    /// Uploads a string of text to Blob storage.
    /// </summary>
    /// <remarks>
    /// Leading and trailing path separators will be removed from the <paramref name="blobName"/>.
    /// </remarks>
    /// <returns>
    /// The URL of the uploaded blob
    /// </returns>
    Task<Uri> UploadString(string blobName, string content);

    /// <summary>
    /// Copies the content from the given <paramref name="url"/>
    /// and uploads it to blob storage with the same path.
    /// <para/>
    /// So if the given url is "http://example.com/a/b/c.txt"
    /// Then it will be uploaded to "...blob-container/a/b/c.txt"
    /// </summary>
    /// <returns>
    /// The URL of the uploaded blob
    /// </returns>
    Task<Uri> CopyFromUrl(Uri url);

    /// <summary>
    /// Returns the URL for a blob of the given <paramref name="blobName"/>.
    /// <para/>
    /// NOTE: The blob may not exist and the URL may not be publicly accessible.
    /// </summary>
    Task<Uri> GetBlobUrl(string blobName);

    /// <summary>
    /// Returns true if a blob with the given name exists in the configured Container.
    /// </summary>
    Task<bool> Exists(string blobName);

    /// <summary>
    /// Returns true if a blob with the given name exists in the configured Container.
    /// </summary>
    bool ExistsBlocking(string blobName);

    /// <summary>
    /// Uploads a string of text to the configured Blob Container and sets
    /// Properties on the blob from the <paramref name="metadata"/> object.
    /// </summary>
    void UploadTextAndMetadataBlocking<TMetadata>(string blobName, string content, TMetadata metadata);

    /// <summary>
    /// Returns the content of the blob of the given <paramref name="blobName"/> 
    /// as a text string.
    /// </summary>
    string GetTextBlocking(string blobName);

    /// <summary>
    /// Returns the last-modified time and the metadata deserialized into the custom object
    /// </summary>
    (DateTimeOffset, TMetadata) GetMetadataBlocking<TMetadata>(string blobName);

    /// <summary>
    /// Delete the blob identified by the given <paramref name="blobName"/> and block until the operation is complete.
    /// </summary>
    void DeleteBlocking(string blobName);

    /// <summary>
    /// Enumerate all blobs in the container to return a count.
    /// </summary>
    /// <remarks>
    /// This is O(N) and may require multiple requests. It can be very slow.
    /// </remarks>
    Task<int> GetBlobCount();

    /// <summary>
    /// Asynchronously enumerates all the blobs in the configured Container
    /// using the given <paramref name="wildcardPattern"/>.
    /// <para/>
    /// The two wildcard characters are `*` and `?`.
    /// `?` matches exactly 1 character.
    /// `*` matches 0 or more characters.
    /// <para/>
    /// Trailing wildcards are the most efficent for Blob storage because
    /// searching is optimized for matching blob names by prefix (i.e. virtual path).
    /// </summary>
    Task<IAsyncEnumerable<string>> EnumerateBlobs(string wildcardPattern);
    // TODO: Pass cancellation token to EnumerateBlobs
    // TODO: Convert to only return IAsyncEnumerable<string> https://stackoverflow.com/a/59690902/483776

    /// <summary>
    /// Enables public read-access for all blobs in the entire container.
    /// It does not enable public enumeration of blobs.
    /// </summary>
    Task EnablePublicAccess();
}
