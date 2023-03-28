// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Newtonsoft.Json;

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
            var stringValue = JsonConvert.SerializeObject(value);
            result.Add(property.Name, stringValue);
        }
        return result;
    }

    public T ConvertToObject<T>(IDictionary<string, string> dictionary)
    {
        T item = Activator.CreateInstance<T>();
        foreach (var property in typeof(T).GetProperties())
        {
            var stringValue = dictionary[property.Name];
            //var value = Convert.ChangeType(stringValue, property.PropertyType);
            //var value = JsonSerializer.Deserialize(stringValue, property.PropertyType);
            var value = JsonConvert.DeserializeObject(stringValue, property.PropertyType);
            property.SetValue(item, value);
        }
        return item;
    }
}
