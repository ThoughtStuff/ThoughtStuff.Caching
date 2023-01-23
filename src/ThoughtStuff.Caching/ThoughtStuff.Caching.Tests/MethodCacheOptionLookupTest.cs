// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class MethodCacheOptionLookupTest
{
    class ClassA { public void Go() { } }
    class ClassB { public void Go() { } }
    class ClassC { public void Go() { } }

    [Theory(DisplayName = "Caching: Method Cache Options"), AutoMoq]
    public void ConfigureOptionsByType(MethodCacheOptionsLookup subject)
    {
        var expectedA = MethodCacheOptionsLookup.DefaultRelativeExpiration;
        var expectedB = TimeSpan.FromSeconds(42);
        var expectedC = TimeSpan.FromDays(13);

        subject
            .AddRelativeExpiration<ClassB>(expectedB)
            .AddRelativeExpiration<ClassC>(expectedC);

        subject.GetRelativeExpiration<ClassA>(a => a.Go())
            .Should().Be(expectedA);
        subject.GetRelativeExpiration<ClassB>(b => b.Go())
            .Should().Be(expectedB);
        subject.GetRelativeExpiration<ClassC>(c => c.Go())
            .Should().Be(expectedC);
    }

    interface IInterfaceD
    {
        int GetValues(int year, string day);
    }

    [Theory(DisplayName = "Caching: Method Cache using Arguments"), AutoMoq]
    public void ConfigureOptionsByArguments(MethodCacheOptionsLookup subject)
    {
        var expected99M = TimeSpan.FromDays(13);
        var expected00W = TimeSpan.FromDays(42);

        subject
            .AddRelativeExpiration<IInterfaceD>(expected99M, x => x.GetValues(1999, "Mon"))
            .AddRelativeExpiration<IInterfaceD>(expected00W, x => x.GetValues(2000, "Wed"));

        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(1999, "Mon"))
            .Should().Be(expected99M);
        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(2000, "Wed"))
            .Should().Be(expected00W);
    }

    [Theory(DisplayName = "Caching: Wildcard Parameter"), AutoMoq]
    public void ConfigureOptionsWithWildcard(MethodCacheOptionsLookup subject)
    {
        var expected99 = TimeSpan.FromDays(99);

        subject
            .AddRelativeExpiration<IInterfaceD>(expected99, x => x.GetValues(1999, AnyArgument.String));

        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(1999, "Mon"))
            .Should().Be(expected99);
        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(1999, "Tues"))
            .Should().Be(expected99);
        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(1999, null))
            .Should().Be(expected99);
    }

    [Theory(DisplayName = "Caching: Variable Parameter"), AutoMoq]
    public void ConfigureOptionsWithVariableParameter(MethodCacheOptionsLookup subject)
    {
        var expected = TimeSpan.FromDays(77);
        var year = 2007;
        const string day = "Th";

        subject
            .AddRelativeExpiration<IInterfaceD>(expected, x => x.GetValues(year, day));

        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(2007, "Th"))
            .Should().Be(expected);
        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(2007, null))
            .Should().Be(MethodCacheOptionsLookup.DefaultRelativeExpiration);
    }

    [Theory(DisplayName = "Caching: Method Cache priority"), AutoMoq]
    public void PrioritizeArgumentMatching(MethodCacheOptionsLookup subject)
    {
        var expectedDefault = TimeSpan.FromDays(2000);
        var expected99M = TimeSpan.FromDays(13);

        subject
            .AddRelativeExpiration<IInterfaceD>(expectedDefault)
            .AddRelativeExpiration<IInterfaceD>(expected99M, x => x.GetValues(1999, "Mon"));

        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(1999, "Mon"))
            .Should().Be(expected99M);
        subject.GetRelativeExpiration<IInterfaceD>(x => x.GetValues(2000, "Wed"))
            .Should().Be(expectedDefault);
    }
}
