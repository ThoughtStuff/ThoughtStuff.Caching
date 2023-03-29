// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Text.Json;

namespace ThoughtStuff.Caching.FileSystem;

public class JsonFileSerializer : IObjectFileSerializer
{
    public void SerializeToFile<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value);
        File.WriteAllText(path, json);
    }

    public T? DeserializeFromFile<T>(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json);
    }
}
