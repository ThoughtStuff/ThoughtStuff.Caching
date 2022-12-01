// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Linq.Expressions;

namespace ThoughtStuff.Caching.Tests;

public class MethodExpressionExtensionsTest
{
    interface IExample
    {
        int GetNumber();
        double Add(double a, double b);
        int Lookup(Uri uri);
    }

    class ExampleClass
    {
        public int GetAge(DateTime birthDate) => throw new NotImplementedException();
    }

    static MethodInvocation GetExampleInvocation(Expression<Action<IExample>> expression) =>
        expression.GetMethodInvocation();

    [Fact(DisplayName = "MethodExpression: Empty on Interface")]
    public void EmptyMethod()
    {
        var methodInvocation = GetExampleInvocation(x => x.GetNumber());

        methodInvocation.MethodInfo.DeclaringType.Should().Be(typeof(IExample));
        methodInvocation.MethodInfo.Name.Should().Be("GetNumber");
        methodInvocation.Arguments.Should().BeEmpty();
    }

    [Fact(DisplayName = "MethodExpression: One Arg on Class")]
    public void OneArg()
    {
        var date = new DateTime(2010, 11, 12);

        var methodInvocation = MethodExpressionExtensions.GetMethodInvocation<ExampleClass>(x => x.GetAge(date));

        methodInvocation.MethodInfo.DeclaringType.Should().Be(typeof(ExampleClass));
        methodInvocation.MethodInfo.Name.Should().Be("GetAge");
        methodInvocation.Arguments.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            { "birthDate", date }
        });
    }

    [Fact(DisplayName = "MethodExpression: Two Constant Args")]
    public void TwoArgs()
    {
        var methodInvocation = GetExampleInvocation(x => x.Add(13, 37));

        methodInvocation.Arguments.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            { "a", 13 },
            { "b", 37 },
        });
    }

    [Fact(DisplayName = "MethodExpression: Any Double")]
    public void AnyDouble()
    {
        var methodInvocation = GetExampleInvocation(x => x.Add(AnyArgument.Double, 37));

        methodInvocation.Arguments.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            { "a", AnyArgument.Placeholder },
            { "b", 37 },
        });
    }

    [Fact(DisplayName = "MethodExpression: Any Uri")]
    public void AnyUri()
    {
        var methodInvocation = GetExampleInvocation(x => x.Lookup(AnyArgument.Uri));

        methodInvocation.Arguments.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            { "uri", AnyArgument.Placeholder }
        });
    }
}
