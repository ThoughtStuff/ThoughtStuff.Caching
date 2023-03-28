// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

#if NETSTANDARD2_0
#pragma warning disable CS8603 // Possible null reference return.
#endif

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ThoughtStuff.Caching.Core;
using System.Runtime.CompilerServices;

namespace ThoughtStuff.Caching.Azure;

public class BlobStorageService : IBlobStorageService
{
    private readonly IOptions<AzureCachingOptions> azureCachingOptions;
    private readonly IObjectDictionaryConverter objectDictionaryConverter;
    private readonly IHttpClientFactory httpClientFactory;

    public BlobStorageService(IOptions<AzureCachingOptions> azureCachingOptions,
                              IObjectDictionaryConverter objectDictionaryConverter,
                              IHttpClientFactory httpClientFactory)
    {
        this.azureCachingOptions = azureCachingOptions ?? throw new ArgumentNullException(nameof(azureCachingOptions));
        this.objectDictionaryConverter = objectDictionaryConverter ?? throw new ArgumentNullException(nameof(objectDictionaryConverter));
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc/>
    public async Task<Uri> UploadStream(string blobName, Stream content)
    {
        blobName = CleanBlobName(blobName);
        var container = await GetBlobContainerClient();
        await container.UploadBlobAsync(blobName, content);
        return GetBlobUrl(blobName, container);
    }

    /// <inheritdoc/>
    public async Task<Uri> UploadString(string blobName, string content)
    {
        // TODO: More efficient method to convert string to stream
        var contentStream = content.ToStream();
        return await UploadStream(blobName, contentStream);
    }

    /// <inheritdoc/>
    public async Task<Uri> CopyFromUrl(Uri url)
    {
        // Alternative method: https://docs.microsoft.com/en-us/azure/storage/blobs/storage-blob-copy?tabs=dotnet#copy-a-blob
        var httpClient = httpClientFactory.CreateClient();
        using var stream = await httpClient.GetStreamAsync(url);
        return await UploadStream(url.AbsolutePath, stream);
    }

    /// <inheritdoc/>
    public async Task<Uri> GetBlobUrl(string blobName)
    {
        blobName = CleanBlobName(blobName);
        var container = await GetBlobContainerClient();
        return GetBlobUrl(blobName, container);
    }

    /// <inheritdoc/>
    public async Task<bool> Exists(string blobName)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        return await blobClient.ExistsAsync();
    }

    /// <inheritdoc/>
    public bool ExistsBlocking(string blobName)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        return blobClient.Exists();
    }

    /// <inheritdoc/>
    public string GetTextBlocking(string blobName)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        BlobDownloadInfo download = blobClient.Download();
        var stream = download.Content;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <inheritdoc/>
    public (DateTimeOffset, TMetadata) GetMetadataBlocking<TMetadata>(string blobName)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        BlobProperties properties = blobClient.GetProperties();
        var metadata = objectDictionaryConverter.ConvertToObject<TMetadata>(properties.Metadata);
        return (properties.LastModified, metadata);
    }

    /// <inheritdoc/>
    public void UploadTextAndMetadataBlocking<TMetadata>(string blobName, string content, TMetadata metadata)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        var contentStream = content.ToStream();
        blobClient.Upload(contentStream, overwrite: true);
        var metadataDictionary = objectDictionaryConverter.ConvertToDictionary(metadata);
        blobClient.SetMetadata(metadataDictionary);
    }

    /// <inheritdoc/>
    public void DeleteBlocking(string blobName)
    {
        var blobClient = GetBlobClientBlocking(blobName);
        blobClient.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots);
    }

    /// <inheritdoc/>
    public async Task<int> GetBlobCount()
    {
        var container = await GetBlobContainerClient();
        var blobsPaged = container.GetBlobsAsync();
        return await blobsPaged
            .AsAsyncEnumerable()
            .CountAsync();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> EnumerateBlobs(string wildcardPattern, CancellationToken cancellationToken = default)
    {
        var wildcardCount = wildcardPattern
            .Count(c => c == '*' || c == '?');
        // No wildcards: Search by exact name
        if (wildcardCount == 0)
        {
            // TODO: Use async Exists here:
            var exists = ExistsBlocking(wildcardPattern);
            if (exists)
                return new[] { wildcardPattern }.ToAsyncEnumerable();
            return AsyncEnumerable.Empty<string>();
        }
        // Trailing '*': Special case because blob storage optimized for searching by prefix
        if (wildcardCount == 1 && wildcardPattern.EndsWith("*"))
        {
            var prefix = wildcardPattern.TrimEnd('*');
            return EnumerateBlobsMatchingPrefix(prefix, cancellationToken);
        }
        // Otherwise: must manually search through all names for matches
        return EnumerateBlobsMatchingWildcards(wildcardPattern, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnablePublicAccess()
    {
        var container = await GetBlobContainerClient();
        await container.SetAccessPolicyAsync(PublicAccessType.Blob);
    }

    /// <summary>
    /// Trims leading & trailing path separators. Throws if null or empty string.
    /// </summary>
    private static string CleanBlobName(string? blobName)
    {
        // Leading the name with path separator has the odd effect of
        // nullifying the entire virtual "path"
        // (at least when viewed in Storage Explorer)
        // So attempting to save blob with name: /foo/bar/baz/example.txt
        // will only save with a name of 'example.txt'
        // It also has the effect of hiding other entries with leading slash!
        blobName = blobName?.Trim('/', '\\');
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException($"'{nameof(blobName)}' cannot be null or whitespace.", nameof(blobName));
        return blobName;
    }

    private static Uri GetBlobUrl(string blobName, BlobContainerClient container)
    {
        var url = container.GetBlobClient(blobName).Uri;
        // HACK: Unescape %2F due to following issue
        // https://github.com/Azure/azure-sdk-for-net/issues/15902
        //return new BlobUriBuilder(url).ToUri();
        var unescaped = url.ToString().Replace("%2F", "/");
        return new Uri(unescaped);
    }

    private async IAsyncEnumerable<string> EnumerateBlobsMatchingPrefix(string prefix,
                                                                        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await GetBlobContainerClient();
        var blobsPaged = container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);
        var names = blobsPaged.AsAsyncEnumerable()
                              .Select(b => b.Name);
        // Iterate so we don't need to return Task<IAsyncEnumerable>
        await foreach (var name in names)
            yield return name;
    }

    private async IAsyncEnumerable<string> EnumerateBlobsMatchingWildcards(string wildcardPattern,
                                                                           [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Can use index of first wildcard to determine common prefix
        var firstWildcard = wildcardPattern.IndexOfAny(new[] { '*', '?' });
        var prefix = wildcardPattern.Substring(0, firstWildcard);
        var regex = StringUtilities.WildcardToRegex(wildcardPattern);
        await foreach (var blobName in EnumerateBlobsMatchingPrefix(prefix, cancellationToken))
        {
            if (regex.IsMatch(blobName))
                yield return blobName;
        }
    }

    internal async Task<BlobContainerClient> GetBlobContainerClient()
    {
        var container = GetBlobContainerClientBlocking();
        if (azureCachingOptions.Value?.CreateBlobContainer == true)
            await container.CreateIfNotExistsAsync();
        return container;
    }

    private BlobContainerClient GetBlobContainerClientBlocking()
    {
        var connectionString = azureCachingOptions.Value?.BlobStorageConnectionString ??
            throw new Exception($"Missing configuration {AzureCachingOptions.Name}.{nameof(AzureCachingOptions.BlobStorageConnectionString)}.");
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerName = azureCachingOptions.Value?.BlobContainerName ??
            throw new Exception($"Missing configuration {AzureCachingOptions.Name}.{nameof(AzureCachingOptions.BlobContainerName)}.");
        // TODO: Validate container name
        // https://docs.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#container-names
        BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
        return container;
    }

    internal BlobClient GetBlobClientBlocking(string blobName)
    {
        blobName = CleanBlobName(blobName);
        var container = GetBlobContainerClientBlocking();
        if (azureCachingOptions.Value?.CreateBlobContainer == true)
            container.CreateIfNotExists();
        var blobClient = container.GetBlobClient(blobName);
        return blobClient;
    }
}
