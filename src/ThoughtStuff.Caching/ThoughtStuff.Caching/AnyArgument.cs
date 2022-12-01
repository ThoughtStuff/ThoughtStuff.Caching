// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;

namespace ThoughtStuff.Caching;

public static class AnyArgument
{
    public static string String => throw DoNotCall();
    public static int Int => throw DoNotCall();
    public static long Long => throw DoNotCall();
    public static double Double => throw DoNotCall();
    public static Uri Uri => throw DoNotCall();
    public static IProgress<double> Progress => throw DoNotCall();

    private static Exception DoNotCall() => new NotSupportedException("Do not call directly. Use within expression.");

    internal static object Placeholder { get; } = new { SpecialPlaceholderArgument = "this object indicates the argument used in a method call expression should be a wild card" };
}
