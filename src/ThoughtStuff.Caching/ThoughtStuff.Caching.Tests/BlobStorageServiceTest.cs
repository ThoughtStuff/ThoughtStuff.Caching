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
        // TODO: Use .BeOneOf after upgrading FluentAssertions to 6+
        before.StatusCode.Should().Match(s => s == HttpStatusCode.NotFound || s == HttpStatusCode.Forbidden);

        await subject.EnablePublicAccess();

        var after = await httpClient.GetAsync(url);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

}
