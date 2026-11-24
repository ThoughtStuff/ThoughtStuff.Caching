using AutoFixture;
using AutoFixture.AutoMoq;

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
