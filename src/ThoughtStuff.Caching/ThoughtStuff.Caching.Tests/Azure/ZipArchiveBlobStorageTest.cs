// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.Xunit2;
using System.IO.Compression;
using ThoughtStuff.Caching.Azure;

namespace ThoughtStuff.Caching.Tests.Azure;

public class ZipArchiveBlobStorageTest
{
    [Theory(DisplayName = "Zip: Upload 1 file"), CacheTest]
    public async Task UploadingOne([Frozen] IBlobStorageService blobStorageService,
                                   ZipArchiveBlobStorage subject,
                                   string content)
    {
        var directory = FileSystemUtilities.GetTemporaryDirectory();
        var fileName = "test.txt";
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(filePath, content);
        var zipFilePath = Path.GetTempFileName();
        File.Delete(zipFilePath);
        ZipFile.CreateFromDirectory(directory, zipFilePath);
        var zipFileStream = File.OpenRead(zipFilePath);
        var blobPathPrefix = "foo/bar/";

        await subject.UploadZipFiles(zipFileStream, blobPathPrefix);

        var expectedBlobName = "foo/bar/test.txt";
        var blobContent = blobStorageService.GetTextBlocking(expectedBlobName);
        blobContent.Should().Be(content);
    }

    [Theory(DisplayName = "Zip: Upload 3 files"), CacheTest]
    public async Task UploadingThree([Frozen] IBlobStorageService blobStorageService,
                                     ZipArchiveBlobStorage subject,
                                     (string fileName, string content)[] items)
    {
        var directory = FileSystemUtilities.GetTemporaryDirectory();
        foreach (var (fileName, content) in items)
        {
            var filePath = Path.Combine(directory, fileName);
            await File.WriteAllTextAsync(filePath, content);
        }
        var zipFilePath = Path.GetTempFileName();
        File.Delete(zipFilePath);
        ZipFile.CreateFromDirectory(directory, zipFilePath);
        var zipFileStream = File.OpenRead(zipFilePath);
        var blobPathPrefix = "alpha/beta/gamma";

        var blobUrls = await subject.UploadZipFiles(zipFileStream, blobPathPrefix);

        blobUrls.Should().HaveCount(3);
        foreach (var (fileName, content) in items)
        {
            // Slash is added to make the prefix a virtual directory
            var expectedBlobName = $"alpha/beta/gamma/{fileName}";
            var blobUrl = blobUrls.SingleOrDefault(u => u.LocalPath.Contains(fileName))?.ToString();
            blobUrl.Should().StartWith("http://127.0.0.1:10000/devstoreaccount1/");
            blobUrl.Should().EndWith(expectedBlobName);
            var blobContent = blobStorageService.GetTextBlocking(expectedBlobName);
            blobContent.Should().Be(content);
        }
    }
}
