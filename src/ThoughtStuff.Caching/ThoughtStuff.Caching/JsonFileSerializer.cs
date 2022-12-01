// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Newtonsoft.Json;
using System.IO;
using ThoughtStuff.Core.Abstractions;

namespace ThoughtStuff.Caching
{
    public class JsonFileSerializer : IObjectFileSerializer
    {
        public void SerializeToFile<T>(string path, T value)
        {
            var json = JsonConvert.SerializeObject(value);
            File.WriteAllText(path, json);
        }

        public T DeserializeFromFile<T>(string path)
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
