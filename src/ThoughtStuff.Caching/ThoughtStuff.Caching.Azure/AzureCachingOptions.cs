// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Azure;

public class AzureCachingOptions
{
    /// <summary>
    /// This name can be used for the configuration section name
    /// </summary>
    public const string Name = nameof(AzureCachingOptions);

    public string? BlobStorageConnectionString { get; set; }
    public string? BlobContainerName { get; set; }

    /// <summary>
    /// Enable to check for the container's existence,
    /// and try to create it if missing, for every operation.
    /// <para/>
    /// Checking the container's existence with every operation 
    /// adds unnecessary overhead for the unusual edge case.
    /// So this should be false except in tests or when
    /// setting up storage the first time.
    /// </summary>
    public bool CreateBlobContainer { get; set; }

    // Empty constructor required for Options pattern
    // so OptionsFactory can create an instance
    public AzureCachingOptions()
    {
    }

    public AzureCachingOptions(string blobStorageConnectionString, string blobContainerName, bool createBlobContainer)
    {
        BlobStorageConnectionString = blobStorageConnectionString ?? throw new ArgumentNullException(nameof(blobStorageConnectionString));
        BlobContainerName = blobContainerName ?? throw new ArgumentNullException(nameof(blobContainerName));
        CreateBlobContainer = createBlobContainer;
    }
}
