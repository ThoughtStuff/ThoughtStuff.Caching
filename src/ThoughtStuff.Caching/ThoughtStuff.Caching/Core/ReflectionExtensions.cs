// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Reflection;

namespace ThoughtStuff.Caching.Core;

internal static class ReflectionExtensions
{
    /// <summary>
    /// Uses reflection to get the value of the named instance property.
    /// The property can be public or private.
    /// </summary>
    /// <typeparam name="T">The type the property value will be cast to</typeparam>
    public static T GetPropertyValue<T>(this object instance, string propertyName)
    {
        var type = instance.GetType();
        var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public
                                                          | BindingFlags.NonPublic
                                                          | BindingFlags.Instance);
        return (T)propertyInfo.GetValue(instance, null);
    }
}
