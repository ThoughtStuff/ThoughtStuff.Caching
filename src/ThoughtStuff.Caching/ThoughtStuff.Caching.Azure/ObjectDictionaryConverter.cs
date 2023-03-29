// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThoughtStuff.Caching.Azure;

public class ObjectDictionaryConverter : IObjectDictionaryConverter
{
    public Dictionary<string, string> ConvertToDictionary<T>(T item)
    {
        var result = new Dictionary<string, string>();
        foreach (var property in typeof(T).GetProperties())
        {
            var value = property.GetValue(item);
            //var stringValue = Convert.ToString(value);
            var stringValue = JsonSerializer.Serialize(value);
            result.Add(property.Name, stringValue);
        }
        return result;
    }

    public T ConvertToObject<T>(IDictionary<string, string> dictionary)
    {
        T item = Activator.CreateInstance<T>();
        JsonSerializerOptions options = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        foreach (var property in typeof(T).GetProperties())
        {
            var stringValue = dictionary[property.Name];
            //var value = Convert.ChangeType(stringValue, property.PropertyType);
            var value = JsonSerializer.Deserialize(stringValue, property.PropertyType, options);
            property.SetValue(item, value);
        }
        return item;
    }
}
