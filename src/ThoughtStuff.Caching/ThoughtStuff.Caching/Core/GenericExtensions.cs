// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Diagnostics.CodeAnalysis;

namespace ThoughtStuff.Caching.Core;

public static class GenericExtensions
{
    /// <summary>
    /// Checks whether an item is default without boxing for primitives.
    /// So for classes will return true if the item is null.
    /// </summary>
    public static bool IsDefault<T>(
#if NETSTANDARD2_1
        [NotNullWhen(returnValue: false)]
#endif
        this T item)
    {
        // https://stackoverflow.com/a/864860/483776
        return EqualityComparer<T?>.Default.Equals(item, default);
    }
}
