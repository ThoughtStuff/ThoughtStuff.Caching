// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Text.RegularExpressions;

namespace ThoughtStuff.Core;

public static class StringUtilities
{
    /// <summary>
    /// Converts a string wildcard pattern to a <see cref="Regex"/>.
    /// The two wildcard characters are `*` and `?`.
    /// `?` matches exactly 1 character.
    /// `*` matches 0 or more characters.
    /// </summary>
    public static Regex WildcardToRegex(string wildcardPattern)
    {
        // See https://www.codeproject.com/Articles/11556/Converting-Wildcards-to-Regexes
        // Escape the wildcard pattern because it might contain other Reg Ex control characters
        var escaped = Regex.Escape(wildcardPattern);
        // Once escaped the wildcards will now be prefixed by a back-slash
        var expression = escaped
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");
        return new Regex($"^{expression}$");
    }

    public static string JoinNonEmpty(string separator, params string[] source)
    {
        var nonEmpty = source.Where(s => s.IsNotEmpty());
        return string.Join(separator, nonEmpty);
    }
}
