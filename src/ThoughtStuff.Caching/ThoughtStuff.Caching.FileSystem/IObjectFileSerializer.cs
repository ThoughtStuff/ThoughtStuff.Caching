namespace ThoughtStuff.Caching.FileSystem;

public interface IObjectFileSerializer
{
    T? DeserializeFromFile<T>(string path);
    void SerializeToFile<T>(string path, T value);
}
