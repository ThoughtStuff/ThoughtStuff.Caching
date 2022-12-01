// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ThoughtStuff.Core
{
    public static class StringExtensions
    {
        // TODO: Unit tests for StringExtensions

        public static Stream ToStream(this string s)
        {
            // TODO: Find implementation that doesn't copy the string https://stackoverflow.com/a/57100948/483776
            byte[] buffer = ToBytes(s);
            return new MemoryStream(buffer);
        }

        public static byte[] ToBytes(string s)
        {
            var encoding = Encoding.UTF8;
            return encoding.GetBytes(s);
        }

        /// <summary>
        /// Remove trailing characters of given count
        /// </summary>
        public static string Truncate(this string s, int count)
        {
            if (s.Length <= count)
                return s;
            return s.Substring(0, s.Length - count);
        }

        public static string RemovePunctuation(this string s)
        {
            // https://stackoverflow.com/a/3063796/483776
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (!char.IsPunctuation(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replaces consecutive white space characters with a single ' ' space
        /// </summary>
        public static string ConsolidateWhiteSpace(this string s)
        {
            return Regex.Replace(s, @"\s+", " ");
        }

        /// <summary>
        /// Returns true if every character is upper case
        /// ignoring white space and punctuation.
        /// </summary>
        public static bool IsAllUpperCase(this string s)
        {
            return s.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c));
        }

        public static string ToTitleCase(this string s)
        {
            // HACK: First convert to all lower case because TextInfo.ToTitleCase won't transform all caps
            // TODO: Recognize known acronyms that are all caps and leave them
            s = s.ToLowerInvariant();
            var titleCase = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s);
            // TODO: More generalized list of small words
            // Handle small words (e.g. articles, prepositions, conjunctions)
            titleCase = titleCase
                .Replace(" A ", " a ")
                .Replace(" At ", " at ")
                .Replace(" Of ", " of ")
                .Replace(" Per ", " per ")
                .Replace(" And ", " and ");
            return titleCase;
        }

        /// <summary>
        /// Returns true of <paramref name="text"/> is null, empty or white space
        /// </summary>
        public static bool IsEmpty(this string text) => string.IsNullOrWhiteSpace(text);

        /// <summary>
        /// Returns true of <paramref name="text"/> is not null, not empty and not only white space
        /// </summary>
        public static bool IsNotEmpty(this string text) => !text.IsEmpty();

        public const char enDash = (char)0x2013;
        public const char emDash = (char)0x2014;
        public const char horizontalBar = (char)0x2015;

        public static bool IsDash(char c)
        {
            // https://devblogs.microsoft.com/powershell/em-dash-en-dash-dash-dash-dash/
            return c == enDash || c == emDash || c == horizontalBar || c == '-';
        }

        /// <summary>
        /// Returns true if the trimmed string is exactly one dash character.
        /// A dash could be a hyphen, em-dash, en-dash or horizontal bar character.
        /// </summary>
        public static bool IsDash(this string s)
        {
            s = s.Trim();
            return s.Length == 1 && IsDash(s[0]);
        }

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

        /// <summary>
        /// Returns true if any of the <paramref name="values"/> is present in the <paramref name="source"/> string
        /// </summary>
        public static bool ContainsAny(this string source, StringComparison stringComparison, params string[] values)
        {
            foreach (var value in values)
            {
                if (source.Contains(value, stringComparison))
                    return true;
            }
            return false;
        }

        public static bool Contains(this string source, string value, StringComparison stringComparison)
        {
            // https://stackoverflow.com/a/444818/483776
            return source?.IndexOf(value, stringComparison) >= 0;
        }
    }
}
