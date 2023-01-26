// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Core;

namespace ThoughtStuff.Caching.FileSystem;

internal sealed class LocalFileCacheManager : ICacheManager
{
    private readonly LocalFileCache localFileCache;

    public LocalFileCacheManager(LocalFileCache localFileCache)
    {
        this.localFileCache = localFileCache;
    }

    /// <inheritdoc/>
    public Task<int> GetCacheEntryCount()
    {
        // https://stackoverflow.com/questions/719020/is-there-an-async-version-of-directoryinfo-getfiles-directory-getdirectories-i
        return Task.Run(() =>
        {
            var directory = new DirectoryInfo(localFileCache.BaseDirectory);
            var files = directory.EnumerateFiles("*.txt");
            return files.Count();
        });
    }

    /// <inheritdoc/>
    public async Task<int> DeleteMatchingEntries(string keyWildcardExpression)
    {
        var count = 0;
        var files = await GetMatchingEntries(keyWildcardExpression);
        foreach (var file in files)
        {
            // https://stackoverflow.com/questions/10606328/why-isnt-there-an-asynchronous-file-delete-in-net
            file.Delete();
            ++count;
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> GetCountOfMatchingEntries(string keyWildcardExpression)
    {
        var matches = await GetMatchingEntries(keyWildcardExpression);
        return matches.Count();
    }

    public Task<IEnumerable<FileInfo>> GetMatchingEntries(string keyWildcardExpression)
    {
        // https://stackoverflow.com/questions/719020/is-there-an-async-version-of-directoryinfo-getfiles-directory-getdirectories-i
        return Task.Run(() =>
        {
            var directory = new DirectoryInfo(localFileCache.BaseDirectory);
            // netstandard 21 and net5 have MatchCasing option
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.enumerationoptions.matchcasing?view=net-5.0#System_IO_EnumerationOptions_MatchCasing
            var searchPattern = LocalFileCache.GetFileName(keyWildcardExpression, keepWildcards: true);
            var files = directory.EnumerateFiles(searchPattern);
            // HACK: Searching directories via .NET not case-sensitive, so case sensitive check here:
            var regex = StringUtilities.WildcardToRegex(searchPattern);
            return files
                .Where(file => regex.IsMatch(file.Name));
        });
    }
}
