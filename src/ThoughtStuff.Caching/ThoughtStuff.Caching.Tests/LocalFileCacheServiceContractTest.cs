// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Caching.FileSystem;

namespace ThoughtStuff.Caching.Tests;

/// <summary>
/// This serves as "black box" test against the ITextCache service contract
/// </summary>
public class LocalFileCacheServiceContractTest : TextCacheTestBase<LocalFileCache>
{
    // TODO: The LocalFileCache constructed by AutoMoq writes to the system directory (not a test directory) and doesn't handle metadata
}
