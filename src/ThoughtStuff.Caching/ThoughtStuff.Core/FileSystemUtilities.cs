// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Core;

public static class FileSystemUtilities
{
    /// <summary>
    /// Returns a new empty directory based on <see cref="Path.GetTempFileName"/>
    /// </summary>
    public static string GetTemporaryDirectory()
    {
        string tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);
        return tempFolder;
    }
}
