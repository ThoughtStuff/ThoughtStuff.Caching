// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Runtime.CompilerServices;

namespace ThoughtStuff.Caching.Azure;

public class AzureBlobTextCacheManager : ICacheManager
{
    private readonly IBlobStorageService blobStorageService;

    public AzureBlobTextCacheManager(IBlobStorageService blobStorageService)
    {
        this.blobStorageService = blobStorageService;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteMatchingEntries(string keyWildcardExpression)
    {
        keyWildcardExpression = AzureBlobTextCache.KeyToBlobName(keyWildcardExpression, keepWildcards: true);
        // TODO: Possible to delete several blobs at once in a single request https://docs.microsoft.com/en-us/dotnet/api/azure.storage.blobs.specialized.blobbatchclient.deleteblobsasync?view=azure-dotnet
        var blobs = blobStorageService.EnumerateBlobs(keyWildcardExpression);
        int count = 0;
        await foreach (var blob in blobs)
        {
            blobStorageService.DeleteBlocking(blob);
            ++count;
        }
        return count;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> EnumerateKeys([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var keys = blobStorageService.EnumerateBlobs("*", cancellationToken);
        await foreach (var key in keys)
        {
            yield return key;
        }
    }

    /// <inheritdoc/>
    public Task<int> GetCacheEntryCount()
    {
        return blobStorageService.GetBlobCount();
    }

    /// <inheritdoc/>
    public async Task<int> GetCountOfMatchingEntries(string keyWildcardExpression)
    {
        keyWildcardExpression = AzureBlobTextCache.KeyToBlobName(keyWildcardExpression, keepWildcards: true);
        var blobs = blobStorageService.EnumerateBlobs(keyWildcardExpression);
        return await blobs.CountAsync();
    }
}
