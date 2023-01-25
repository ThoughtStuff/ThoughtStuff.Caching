// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using ThoughtStuff.Caching.Azure;
using ThoughtStuff.Caching.FileSystem;
using ThoughtStuff.Core.Abstractions;
using static ThoughtStuff.Core.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests;

public class CacheTestAttribute : AutoDataAttribute
{
    public CacheTestAttribute()
        : base(() => BuildFixture())
    {
    }

    internal static IFixture BuildFixture()
    {
        var fixture = new Fixture();
        fixture.Register<ICacheExpirationService>(() => fixture.Create<CacheExpirationService>());
        fixture.Register<IDefaultCachePolicyService>(() => fixture.Create<HardCodedDefaultCachePolicy>());
        fixture.Register<ITextCache>(() => fixture.Create<DictionaryTextCache>());
        ConfigureLocalFileCache(fixture);
        ConfigureAzureCaching(fixture);
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true });
        return fixture;
    }

    /// <summary>
    /// Cenfiguration for a LocalFileCache that will use an empty temp directory for each test
    /// </summary>
    private static void ConfigureLocalFileCache(Fixture fixture)
    {
        fixture.Register(() =>
            new LocalFileCacheOptions
            {
                BaseDirectory = GetTemporaryDirectory()
            });
        // Register serializer used for metadata
        fixture.Register<IObjectFileSerializer>(() => fixture.Create<JsonFileSerializer>());
    }

    /// <summary>
    /// Register Blob Storage options to use local storage emulator
    /// </summary>
    private static void ConfigureAzureCaching(Fixture fixture)
    {
        fixture.Register((string containerName) =>
        {
            var options = new AzureCachingOptions
            {
                BlobStorageConnectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;",
                BlobContainerName = containerName.ToLowerInvariant(),
                CreateBlobContainer = true
            };
            return options;
        });
        // Metadata conversion requires a real IObjectDictionaryConverter
        fixture.Register<IObjectDictionaryConverter>(() => fixture.Create<ObjectDictionaryConverter>());
        // Use real blob storage when requested
        fixture.Register<IBlobStorageService>(() => fixture.Create<BlobStorageService>());
    }
}
