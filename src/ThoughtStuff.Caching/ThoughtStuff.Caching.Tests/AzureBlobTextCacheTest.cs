// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture;
using ThoughtStuff.Caching.Azure;

namespace ThoughtStuff.Caching.Tests;

public class AzureBlobTextCacheTest : TextCacheTestBase<AzureBlobTextCache>
{
    [Theory(DisplayName = "Blob Cache: Blob Names")]
    [InlineData("a", "a")]
    // Can start with a number
    [InlineData("1", "1")]
    [InlineData("2b", "2b")]
    // Should not end in period
    [InlineData("a.", "a_")]
    // Should be trimmed
    [InlineData("  abc   ", "abc")]
    // Is case sensitive
    [InlineData("AbCd", "AbCd")]
    // Method-based names
    [InlineData("Foo('bar')", "Foo/'bar'")]
    [InlineData("Foo('bar','baz')", "Foo/'bar'/'baz'")]
    [InlineData("Foo('(((','baz')", "Foo/'((('/'baz'")]
    [InlineData("Add(1.2,3.)", "Add/1.2/3_")]
    [InlineData("Foo()", "Foo_")]
    // URL & Special Characters
    [InlineData("a?b", "a_b")]
    [InlineData("a*b*", "a_b_")]
    [InlineData("a/b", "a/b")]
    [InlineData("/a/b", "/a/b")]
    [InlineData("/a/b/", "/a/b_")]
    [InlineData("/", "_")]
    [InlineData(@"a\b", "a/b")]
    [InlineData(@"\a\b", "/a/b")]
    [InlineData(@"\a\b\", "/a/b_")]
    [InlineData("a//b", "a__b")]
    [InlineData("a///b", "a__/b")]
    [InlineData(@"a\\\b", "a__/b")]
    [InlineData(@"a\/\/b", "a____b")]
    [InlineData("https://a", "https:__a")]
    public async Task BlobName(string key, string blobName)
    {
        AzureBlobTextCache.KeyToBlobName(key)
            .Should().Be(blobName);
        // Verify it is indeed a valid blob name
        var fixture = CacheTestAttribute.BuildFixture();
        var blobStorage = fixture.Create<BlobStorageService>();
        await blobStorage.UploadString(blobName, blobName);
        blobStorage.GetTextBlocking(blobName).Should().Be(blobName);
    }

    [Theory(DisplayName = "Blob Cache: Blob Names w/ Wildcards")]
    [InlineData("a", "a")]
    [InlineData("a.", "a_")]
    [InlineData("  abc   ", "abc")]
    // Method-based names
    [InlineData("Foo()", "Foo_")]
    [InlineData("Foo(*", "Foo?*")]
    [InlineData("Foo(?", "Foo??")]
    [InlineData("Foo(b(),*", "Foo/b()/*")]
    [InlineData("Go(up,down*", "Go/up/down*")]
    [InlineData("Go(up,down,*", "Go/up/down/*")]
    [InlineData("a?b", "a?b")]
    [InlineData("a*b", "a*b")]
    [InlineData("a/b", "a/b")]
    [InlineData("/", "_")]
    [InlineData(@"\a\b\*", "/a/b/*")]
    public void BlobNameWildcards(string key, string blobName)
    {
        // Wildcards aide searching
        AzureBlobTextCache.KeyToBlobName(key, keepWildcards: true)
            .Should().Be(blobName);
    }
}
