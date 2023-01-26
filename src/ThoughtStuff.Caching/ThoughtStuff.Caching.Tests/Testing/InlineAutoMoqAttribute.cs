// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.Xunit2;

namespace ThoughtStuff.Caching.Tests.Testing;

/// <summary>
/// Allows use of Inline data for some parameters with Moq generation of remaining parameters.
/// Declare inline data parameters first.
/// </summary>
public class InlineAutoMoqAttribute : CompositeDataAttribute
{
    public InlineAutoMoqAttribute(params object?[] values)
        : base(
              new InlineDataAttribute(values),
              new AutoMoqAttribute()
            )
    {
    }
}
