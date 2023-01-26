// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Caching.Core;

namespace ThoughtStuff.Caching.Tests;

public class GenericExtensionsTest
{
    [Fact(DisplayName = "IsDefault: int")]
    public void IsDefaultInt()
    {
        int zero = 0;
        zero.IsDefault().Should().BeTrue();
        int nonZero = 42;
        nonZero.IsDefault().Should().BeFalse();
    }

    [Fact(DisplayName = "IsDefault: float")]
    public void IsDefaultFloat()
    {
        float zero = 0f;
        zero.IsDefault().Should().BeTrue();
        float nonZero = 4.2f;
        nonZero.IsDefault().Should().BeFalse();
    }

    [Fact(DisplayName = "IsDefault: string")]
    public void IsDefaultString()
    {
        string? nullString = null;
        nullString.IsDefault().Should().BeTrue();
        string empty = string.Empty;
        empty.IsDefault().Should().BeFalse();
        string text = "some text";
        text.IsDefault().Should().BeFalse();
    }

    [Fact(DisplayName = "IsDefault: object")]
    public void IsDefaultObject()
    {
        object? nullObject = null;
        nullObject.IsDefault().Should().BeTrue();
        object obj = new();
        obj.IsDefault().Should().BeFalse();
    }
}
