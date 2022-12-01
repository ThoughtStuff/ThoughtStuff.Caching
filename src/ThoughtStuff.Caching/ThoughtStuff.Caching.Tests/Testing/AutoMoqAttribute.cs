// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace ThoughtStuff.Caching.Tests.Testing;

public class AutoMoqAttribute : AutoDataAttribute
{
    public AutoMoqAttribute()
        : base(BuildFixture)
    {
    }

    public static IFixture BuildFixture()
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true });
        return fixture;
    }
}
