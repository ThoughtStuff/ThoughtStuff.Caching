// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Azure;

/// <summary>
/// Converts between a Dictionary of strings and an object
/// </summary>
public interface IObjectDictionaryConverter
{
    Dictionary<string, string> ConvertToDictionary<T>(T item);
    T ConvertToObject<T>(IDictionary<string, string> dictionary);
}
