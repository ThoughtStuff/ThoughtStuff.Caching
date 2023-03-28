// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.Xunit2;
using Azure;
using Azure.Storage.Blobs.Models;
using System.Net;
using ThoughtStuff.Caching.Azure;
using ThoughtStuff.Caching.Core;

namespace ThoughtStuff.Caching.Tests;

public class BlobStorageServiceTest
{
    [Theory(DisplayName = "Blob: Upload String"), CacheTest]
    public async Task BlobUploading(BlobStorageService subject,
                                    string blobName,
                                    string content)
    {
        var blobUrl = await subject.UploadString(blobName, content);

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        BlobDownloadInfo download = await blobClient.DownloadAsync();
        var streamReader = new StreamReader(download.Content);
        var returnedContent = await streamReader.ReadToEndAsync();
        returnedContent.Should().Be(content);
        blobUrl.Should().Be(blobClient.Uri);
    }

    [Theory(DisplayName = "Blob: Upload Stream"), CacheTest]
    public async Task StreamUpload(BlobStorageService subject,
                                   string blobName,
                                   string content)
    {
        var stream = content.ToStream();

        var blobUrl = await subject.UploadStream(blobName, stream);

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        BlobDownloadInfo download = await blobClient.DownloadAsync();
        var streamReader = new StreamReader(download.Content);
        var returnedContent = await streamReader.ReadToEndAsync();
        returnedContent.Should().Be(content);
        blobUrl.Should().Be(blobClient.Uri);
    }

    [Theory(DisplayName = "Blob: Copy from URL"), CacheTest]
    public async Task UrlUpload([Frozen] Mock<IHttpClientFactory> httpClientFactory,
                                BlobStorageService subject,
                                string content)
    {
        var url = new Uri("https://example.com/path/to/file.txt");
        httpClientFactory.SetupHttpClient(url, content);

        var blobUrl = await subject.CopyFromUrl(url);

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient("path/to/file.txt");
        BlobDownloadInfo download = await blobClient.DownloadAsync();
        var streamReader = new StreamReader(download.Content);
        var returnedContent = await streamReader.ReadToEndAsync();
        returnedContent.Should().Be(content);
        //blobUrl.Should().Be(blobClient.Uri);
        blobUrl.AbsolutePath.Should().EndWith("/path/to/file.txt");
    }

    [Theory(DisplayName = "Blob: Get URL"), CacheTest]
    public async Task GettingUrl(BlobStorageService subject,
                                 string blobName,
                                 string content)
    {
        var uploadUrl = await subject.UploadString(blobName, content);

        var returnedUrl = await subject.GetBlobUrl(blobName);

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        returnedUrl.Should().Be(blobClient.Uri)
            .And.Be(uploadUrl);
    }

    [Theory(DisplayName = "Blob: Get URL Trimmed"), CacheTest]
    public async Task GettingUrlClean(BlobStorageService subject,
                                      string blobName,
                                      string content)
    {
        var uploadUrl = await subject.UploadString(blobName, content);

        var returnedUrl = await subject.GetBlobUrl("/" + blobName + "/");

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        returnedUrl.Should().Be(blobClient.Uri)
            .And.Be(uploadUrl);
    }

    [Theory(DisplayName = "Blob: Exists False"), CacheTest]
    public void ExistsFalse(BlobStorageService subject,
                            string blobName)
    {
        subject.ExistsBlocking(blobName)
            .Should().BeFalse();
    }

    [Theory(DisplayName = "Blob: Exists True"), CacheTest]
    public async Task ExistsTrue(BlobStorageService subject,
                                 string blobName)
    {
        await subject.UploadString(blobName, string.Empty);

        subject.ExistsBlocking(blobName)
            .Should().BeTrue();
    }

    [Theory(DisplayName = "Blob: Leading Slash"), CacheTest]
    public async Task LeadingSlash(BlobStorageService subject, string content)
    {
        var blobName = "foo/bar/baz/wha.txt";

        // Upload with a leading slash
        await subject.UploadString("/" + blobName, content);

        subject.ExistsBlocking(blobName).Should().BeTrue();
        subject.GetTextBlocking(blobName).Should().Be(content);
    }

    [Theory(DisplayName = "Blob: Leading Backslash"), CacheTest]
    public async Task LeadingBackslash(BlobStorageService subject, string content)
    {
        var blobName = @"foo\bar\baz\wha.txt";

        // Upload with a leading backslash
        await subject.UploadString("\\" + blobName, content);

        subject.ExistsBlocking(blobName).Should().BeTrue();
        subject.GetTextBlocking(blobName).Should().Be(content);
    }

    [Theory(DisplayName = "Blob: Trailing Slash"), CacheTest]
    public async Task TrailingSlash(BlobStorageService subject, string content)
    {
        var blobName = "foo/bar/baz";

        // Upload with a trailing slash
        await subject.UploadString(blobName + "/", content);

        subject.ExistsBlocking(blobName).Should().BeTrue();
        subject.GetTextBlocking(blobName).Should().Be(content);
    }

    [Theory(DisplayName = "Blob: Upload Leading Backslash"), CacheTest]
    public async Task TrailingBackslash(BlobStorageService subject, string content)
    {
        var blobName = @"foo\bar\baz";

        // Upload with a trailing backslash
        await subject.UploadString(blobName + "\\", content);

        subject.ExistsBlocking(blobName).Should().BeTrue();
        subject.GetTextBlocking(blobName).Should().Be(content);
    }

    [Theory(DisplayName = "Blob: Delete Blocking"), CacheTest]
    public async Task BlobDeleteBlocking(BlobStorageService subject,
                            string blobName,
                            string content)
    {
        await subject.UploadString(blobName, content);

        subject.DeleteBlocking(blobName);

        var blobClient = subject.GetBlobClientBlocking(blobName);
        bool exists = blobClient.Exists();
        exists.Should().BeFalse();
    }

    [Theory(DisplayName = "Blob: Count"), CacheTest]
    public async Task Counting(BlobStorageService subject)
    {
        var expected = 13;
        for (int i = 0; i < expected; i++)
        {
            await subject.UploadString(i.ToString(), string.Empty);
        }

        var count = await subject.GetBlobCount();

        count.Should().Be(expected);
    }

    [Theory(DisplayName = "Blob: Enumerate All"), CacheTest]
    public async Task Enumerating(BlobStorageService subject)
    {
        var count = 7;
        for (int i = 0; i < count; i++)
        {
            await subject.UploadString(i.ToString(), string.Empty);
        }

        int expected = 0;
        await foreach (var blobName in subject.EnumerateBlobs("*"))
        {
            blobName.Should().Be(expected.ToString());
            ++expected;
        }
        expected.Should().Be(count);
    }

    [Theory(DisplayName = "Blob: Enumerate w/ Prefix (trailing wildcard)"), CacheTest]
    public async Task EnumeratePrefix(BlobStorageService subject)
    {
        await subject.UploadString("alpha", string.Empty);
        await subject.UploadString("beta", string.Empty);
        await subject.UploadString("alf", string.Empty);
        await subject.UploadString("gamma", string.Empty);
        await subject.UploadString("alphabet", string.Empty);

        subject.EnumerateBlobs("al*").ToEnumerable().Should().HaveCount(3);
        subject.EnumerateBlobs("alph*").ToEnumerable().Should().HaveCount(2);
        subject.EnumerateBlobs("al?").ToEnumerable().Should().HaveCount(1);
        subject.EnumerateBlobs("bal?").ToEnumerable().Should().HaveCount(0);
    }

    [Theory(DisplayName = "Blob: Enumerate w/ internal wildcards"), CacheTest]
    public async Task EnumerateWildcard(BlobStorageService subject)
    {
        await subject.UploadString("super", string.Empty);
        await subject.UploadString("upper", string.Empty);
        await subject.UploadString("superior", string.Empty);
        await subject.UploadString("superb", string.Empty);
        await subject.UploadString("per", string.Empty);
        await subject.UploadString("x", string.Empty);

        subject.EnumerateBlobs("*per*").ToEnumerable().Should().HaveCount(5);
        subject.EnumerateBlobs("super*").ToEnumerable().Should().HaveCount(3);
        subject.EnumerateBlobs("?up*").ToEnumerable().Should().HaveCount(3);
        subject.EnumerateBlobs("p?r").ToEnumerable().Should().HaveCount(1);
        subject.EnumerateBlobs("p*r").ToEnumerable().Should().HaveCount(1);
        subject.EnumerateBlobs("*up*er*").ToEnumerable().Should().HaveCount(4);
        subject.EnumerateBlobs("s*b").ToEnumerable().Should().HaveCount(1);
        subject.EnumerateBlobs("s?b").ToEnumerable().Should().HaveCount(0);
    }

    [Theory(DisplayName = "Blob: Enumerate w/ Exact match"), CacheTest]
    public async Task EnumerateExact(BlobStorageService subject, string name, string[] random)
    {
        await subject.UploadString(random[0], string.Empty);
        await subject.UploadString(name, string.Empty);
        await subject.UploadString(random[1], string.Empty);

        subject.EnumerateBlobs(name).ToEnumerable().Should().HaveCount(1);
        subject.EnumerateBlobs("x").ToEnumerable().Should().HaveCount(0);
    }

    [Theory(DisplayName = "Blob: Enumerate w/ Cancellation", Skip = "Figure out why this doesn't work"), CacheTest]
    public async Task EnumerateAndCancel(BlobStorageService subject)
    {
        var count = 50;
        for (int i = 0; i < count; i++)
        {
            await subject.UploadString(i.ToString(), string.Empty);
        }

        int j = 0;
        CancellationTokenSource tokenSource = new();
        await foreach (var blobName in subject.EnumerateBlobs("*").WithCancellation(tokenSource.Token))
        {
            ++j;
            if (j == 5)
                tokenSource.Cancel(true);
        }
        j.Should().Be(5);
    }

    public class ExampleMetadata
    {
        public int Count { get; set; }
    }

    [Theory(DisplayName = "Blob: Upload Text w/ Metadata"), CacheTest]
    public async Task UploadTextMeta(BlobStorageService subject,
                               string blobName,
                               string content,
                               ExampleMetadata metadata)
    {
        subject.UploadTextAndMetadataBlocking(blobName, content, metadata);

        subject.ExistsBlocking(blobName)
            .Should().BeTrue();

        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        BlobDownloadInfo download = await blobClient.DownloadAsync();
        var streamReader = new StreamReader(download.Content);
        var returnedContent = await streamReader.ReadToEndAsync();
        returnedContent.Should().Be(content);
        BlobProperties properties = blobClient.GetProperties();
        properties.Metadata.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                ["Count"] = metadata.Count.ToString(),
            });
    }

    [Theory(DisplayName = "Blob: Get Metadata"), CacheTest]
    public async Task GetMetadata(BlobStorageService subject,
                                  string blobName,
                                  int count)
    {
        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        blobClient.Upload(string.Empty.ToStream());
        await blobClient.SetMetadataAsync(new Dictionary<string, string>
        {
            ["Count"] = count.ToString()
        });

        var (modified, metadata) = subject.GetMetadataBlocking<ExampleMetadata>(blobName);

        metadata.Count.Should().Be(count);
        modified.Should().BeBefore(DateTimeOffset.Now)
            .And.BeCloseTo(DateTimeOffset.Now, TimeSpan.FromMilliseconds(3500));
    }

    [Theory(DisplayName = "Blob: Get Text"), CacheTest]
    public async Task GetText(BlobStorageService subject,
                              string blobName,
                              string content)
    {
        var container = await subject.GetBlobContainerClient();
        var blobClient = container.GetBlobClient(blobName);
        blobClient.Upload(content.ToStream());

        var text = subject.GetTextBlocking(blobName);

        text.Should().Be(content);
    }

    [Theory(DisplayName = "Blob: Get Text Missing"), CacheTest]
    public void GetTextMissing(BlobStorageService subject,
                              string blobName)
    {
        Action act = () => subject.GetTextBlocking(blobName);

        act.Should().Throw<RequestFailedException>()
            .WithMessage("The specified blob does not exist.*");
        // Azure.RequestFailedException : The specified blob does not exist.
        // RequestId:6fda21c0-cd9f-45fd-8c2e-a445576e1ec8
        // Time:2021-03-18T14:41:57.8185734Z
        // Status: 404 (The specified blob does not exist.)
        // ErrorCode: BlobNotFound

        // Headers:
        // Server: Windows-Azure-Blob/1.0,Microsoft-HTTPAPI/2.0
        // x-ms-request-id: 6fda21c0-cd9f-45fd-8c2e-a445576e1ec8
        // x-ms-version: 2020-04-08
        // x-ms-error-code: BlobNotFound
        // Date: Thu, 18 Mar 2021 14:41:57 GMT
        // Content-Length: 215
        // Content-Type: application/xml
    }

    [Theory(DisplayName = "Blob: Public Access"), CacheTest]
    public async Task PublicAccess(BlobStorageService subject,
                                   string blobName,
                                   string content)
    {
        // HACK: Creating a real HttpClient to validate the public access
        using var httpClient = new HttpClient();
        await subject.UploadString(blobName, content);
        var url = await subject.GetBlobUrl(blobName);

        var before = await httpClient.GetAsync(url);
        // Azurite returns 403, but Storage Emulator returns 404
        before.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

        await subject.EnablePublicAccess();

        var after = await httpClient.GetAsync(url);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

}
