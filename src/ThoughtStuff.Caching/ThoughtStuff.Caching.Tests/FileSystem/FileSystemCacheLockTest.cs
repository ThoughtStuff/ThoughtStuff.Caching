using Microsoft.Extensions.DependencyInjection;
using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public sealed class FileSystemCacheLockTest : IDisposable
{
    private const string SeedKey = "seed";
    private const string SeedValue = "cached input";
    private readonly string directory = GetTemporaryDirectory();

    public interface IReadThroughService
    {
        /// <summary>Reads a second entry in the same filesystem cache.</summary>
        string Read();
    }

    public sealed class ReadThroughService(ITextCache cache) : IReadThroughService
    {
        /// <inheritdoc/>
        public string Read() =>
            // A different thread must enter the directory gate before this fill can finish.
            Task.Run(() => cache.GetString(SeedKey)!)
                .WaitAsync(TimeSpan.FromSeconds(5))
                .GetAwaiter()
                .GetResult();
    }

    [Fact(DisplayName = "Filesystem locking: The method proxy requests its key lock outside the directory gate")]
    public void LockOrder()
    {
        var gate = LocalFileCacheDirectory.Get(directory).Gate;
        var locks = new CacheLockProvider();
        var observedLocks = new Mock<ICacheLockProvider>();
        observedLocks.Setup(provider => provider.GetCacheLockObject(It.IsAny<string>()))
                     .Returns((string key) =>
                     {
                         Monitor.IsEntered(gate).Should().BeFalse();
                         return locks.GetCacheLockObject(key);
                     });
        using var services = CreateServices().AddSingleton(observedLocks.Object).BuildServiceProvider();
        var proxy = SeedAndResolve(services);

        proxy.Read();

        observedLocks.Verify(provider => provider.GetCacheLockObject(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "Filesystem locking: A cache fill can wait for another thread to read the same directory")]
    public void FillGate()
    {
        using var services = CreateServices().BuildServiceProvider();
        var proxy = SeedAndResolve(services);

        var value = proxy.Read();

        value.Should().Be(SeedValue);
    }

    private IServiceCollection CreateServices() =>
        new ServiceCollection().AddLogging()
                               .AddMethodCaching()
                               .AddLocalFileTextCache(options =>
                               {
                                   options.BaseDirectory = directory;
                                   options.SoftSizeLimitBytes = 4 * 1024;
                               })
                               .AddTransientWithCaching<IReadThroughService, ReadThroughService, string>();

    private static IReadThroughService SeedAndResolve(IServiceProvider services)
    {
        services.GetRequiredService<ITextCache>().SetString(SeedKey, SeedValue);
        return services.GetRequiredService<IReadThroughService>();
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
