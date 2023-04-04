// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ThoughtStuff.Caching.Azure;

namespace ThoughtStuff.Caching.Tests.Azure;

public class AzureBlobCacheIntegrationTest
{
    public interface IExampleService
    {
        Dictionary<string, string> GetInfo(string name);
    }

    class ExampleService : IExampleService
    {
        public Dictionary<string, string> GetInfo(string name)
        {
            return new Dictionary<string, string>
            {
                {"name", name},
                { "age", "42" },
                // Store a date using high precision to detect if called twice
                { "now", DateTime.Now.ToString("O") },
            };
        }
    }

    [Theory(DisplayName = "services.AddCachingWithAzureBlobs"), AutoMoq]
    public void UsingBlobCache(string thoughtStuffContainer)
    {
        // Configure method caching with local JSON text file cache
        var services = new ServiceCollection();
        services.AddCachingWithAzureBlobs(options =>
        {
            // https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata#container-names
            // Must be all lower case
            options.BlobContainerName = thoughtStuffContainer.ToLowerInvariant();
            options.BlobStorageConnectionString = CacheTestAttribute.BlobStorageConnectionString;
            options.CreateBlobContainer = true;
        });
        services.AddMethodCaching()
                .AddLogging()
                .AddTransientWithCaching<IExampleService, ExampleService, Dictionary<string, string>>();
        var serviceProvider = services.BuildServiceProvider();

        // Fetch and use the IExampleService (which should be a caching proxy)
        var exampleService = serviceProvider.GetRequiredService<IExampleService>();
        var info = exampleService.GetInfo("Megatron");

        // Verify that the result was cached to the blob storage as JSON
        var blobStorage = serviceProvider.GetRequiredService<IBlobStorageService>();
        var json = blobStorage.GetTextBlocking("IExampleService.GetInfo/'Megatron'");
        var serialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        // Remove $id which is serialized for preserving references
        serialized.Remove("$id");
        serialized.Should().BeEquivalentTo(info);

        // Call Service again to verify cached
        var info2 = exampleService.GetInfo("Megatron");
        info2.Should().BeEquivalentTo(info);
    }
}
