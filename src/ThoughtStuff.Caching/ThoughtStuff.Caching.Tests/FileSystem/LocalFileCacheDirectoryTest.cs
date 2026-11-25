using ThoughtStuff.Caching.FileSystem;
using static ThoughtStuff.Caching.Tests.Testing.FileSystemUtilities;

namespace ThoughtStuff.Caching.Tests.FileSystem;

public sealed class LocalFileCacheDirectoryTest : IDisposable
{
    private readonly string directory = GetTemporaryDirectory();

    [Fact(DisplayName = "Filesystem directory: Equivalent absolute paths share the coordinator")]
    public void NormalizedPath()
    {
        var alternatePath = Path.Combine(directory, ".") + Path.DirectorySeparatorChar;

        var original = LocalFileCacheDirectory.Get(directory);
        var alternate = LocalFileCacheDirectory.Get(alternatePath);

        alternate.Should().BeSameAs(original);
    }

    [Fact(DisplayName = "Filesystem directory: Concurrent lookups return the same coordinator")]
    public async Task ConcurrentLookup()
    {
        const int callerCount = 8;
        var cancellationToken = TestContext.Current.CancellationToken;

        var lookups = Enumerable.Range(0, callerCount)
                                .Select(_ => Task.Run(() => LocalFileCacheDirectory.Get(directory), cancellationToken));
        var coordinators = await Task.WhenAll(lookups);

        coordinators.Distinct().Should().ContainSingle();
    }

    [Fact(DisplayName = "Filesystem directory: Case variants share a coordinator only when the filesystem aliases them")]
    public void CaseVariants()
    {
        var firstPath = Path.Combine(directory, "Cache");
        var secondPath = Path.Combine(directory, "cache");
        Directory.CreateDirectory(firstPath);
        var sameDirectory = Directory.Exists(secondPath);
        Directory.CreateDirectory(secondPath);

        var first = LocalFileCacheDirectory.Get(firstPath);
        var second = LocalFileCacheDirectory.Get(secondPath);

        ReferenceEquals(first, second).Should().Be(sameDirectory);
    }

    [Fact(DisplayName = "Filesystem directory: A visible probe identifies the existing coordinator")]
    public void VisibleProbe()
    {
        var coordinator = new LocalFileCacheDirectory(directory);
        var alternatePath = Path.Combine(directory, ".");

        var match = LocalFileCacheDirectory.FindAlias(alternatePath, [coordinator]);

        match.Should().BeSameAs(coordinator);
    }

    [Fact(DisplayName = "Filesystem directory: A separate directory cannot see the probe")]
    public void SeparateDirectory()
    {
        var otherDirectory = Directory.CreateDirectory(Path.Combine(directory, "other")).FullName;
        var coordinator = new LocalFileCacheDirectory(otherDirectory);

        var match = LocalFileCacheDirectory.FindAlias(directory, [coordinator]);

        match.Should().BeNull();
    }

    [Fact(DisplayName = "Filesystem directory: Filename casing comes from the directory without an ownership marker")]
    public void FileCasing()
    {
        File.WriteAllText(Path.Combine(directory, "CaseWitness"), "");
        var ignoresCase = File.Exists(Path.Combine(directory, "casewitness"));
        var coordinator = new LocalFileCacheDirectory(directory);

        coordinator.DetectPathCaseSensitivity();

        coordinator.PathsIgnoreCase.Should().Be(ignoresCase);
    }

    [Fact(DisplayName = "Filesystem directory: A successful probe removes its temporary file")]
    public void ProbeCleanup()
    {
        const string persistedFile = "existing.txt";
        File.WriteAllText(Path.Combine(directory, persistedFile), "");

        var wasVisible = LocalFileCacheDirectory.WithProbeFile(directory, name =>
            File.Exists(Path.Combine(directory, name)));

        wasVisible.Should().BeTrue();
        Directory.EnumerateFiles(directory).Select(Path.GetFileName).Should().Equal(persistedFile);
    }

    [Fact(DisplayName = "Filesystem directory: A throwing probe removes its temporary file")]
    public void FailedProbeCleanup()
    {
        var failure = new IOException("Probe check failed");

        var thrown = Record.Exception(() => LocalFileCacheDirectory.WithProbeFile<bool>(directory, _ => throw failure));

        thrown.Should().BeSameAs(failure);
        Directory.EnumerateFiles(directory).Should().BeEmpty();
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
