// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public class FileSystemCacheIntegrationTest
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

    [Theory(DisplayName = "services.AddLocalFileTextCache"), AutoMoq]
    public void AddingFileCache(string directoryName)
    {
        // Configure method caching with local JSON text file cache
        var baseDirectory = Path.Combine(Path.GetTempPath(), "ThoughtStuff", directoryName);
        var services = new ServiceCollection();
        services.AddLocalFileTextCache(options => options.BaseDirectory = baseDirectory)
                .AddMethodCaching()
                .AddLogging()
                .AddTransientWithCaching<IExampleService, ExampleService, Dictionary<string, string>>();
        var serviceProvider = services.BuildServiceProvider();

        // Fetch and use the IExampleService (which should be a caching proxy)
        var exampleService = serviceProvider.GetRequiredService<IExampleService>();
        var info = exampleService.GetInfo("Megatron");

        // Verify that the result was cached to the file system as JSON
        var json = File.ReadAllText(Path.Combine(baseDirectory, "IExampleService.GetInfo('Megatron').txt"));
        var serialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        // Remove $id which is serialized for preserving references
        serialized.Remove("$id");
        serialized.Should().BeEquivalentTo(info);

        // Call Service again to verify cached
        var info2 = exampleService.GetInfo("Megatron");
        info2.Should().BeEquivalentTo(info);
    }
}
