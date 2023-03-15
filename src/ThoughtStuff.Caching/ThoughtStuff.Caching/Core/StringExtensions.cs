// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ThoughtStuff.Caching.Core;

public static class StringExtensions
{
    // TODO: Unit tests for StringExtensions

    public static Stream ToStream(this string s)
    {
        // TODO: Find implementation that doesn't copy the string https://stackoverflow.com/a/57100948/483776
        byte[] buffer = ToBytes(s);
        return new MemoryStream(buffer);
    }

    internal static byte[] ToBytes(string s)
    {
        var encoding = Encoding.UTF8;
        return encoding.GetBytes(s);
    }

    /// <summary>
    /// Returns true of <paramref name="text"/> is null, empty or white space
    /// </summary>
    public static bool IsEmpty(
#if NETSTANDARD2_1
        [NotNullWhen(returnValue: false)]
#endif
        this string? text) => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Returns true of <paramref name="text"/> is not null, not empty and not only white space
    /// </summary>
    public static bool IsNotEmpty(
#if NETSTANDARD2_1
        [NotNullWhen(returnValue: true)]
#endif
        this string? text) => !text.IsEmpty();

    /// <summary>
    /// Replace the first occurence of <paramref name="oldValue"/> 
    /// with <paramref name="newValue"/> in the given <paramref name="source"/> string.
    /// </summary>
    public static string ReplaceFirst(this string source, string oldValue, string newValue)
    {
        // https://stackoverflow.com/a/8809437/483776
        int pos = source.IndexOf(oldValue);
        if (pos < 0)
        {
            return source;
        }
        return source.Substring(0, pos) + newValue + source.Substring(pos + oldValue.Length);
    }
}
