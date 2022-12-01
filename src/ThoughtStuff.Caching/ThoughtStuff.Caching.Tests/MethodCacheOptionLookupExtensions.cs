// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using System.Linq.Expressions;
// Moq also has an IInvocation, so disambiguate:
using Invocation = Castle.DynamicProxy.IInvocation;

namespace ThoughtStuff.Caching.Tests;

internal static class MockInvocation
{
    /// <summary>
    /// Create a mock <see cref="Invocation"/>
    /// from a lambda expression
    /// </summary>
    public static Invocation Of<T>(Expression<Action<T>> expression) =>
        ForLambda(expression);

    private static Invocation ForLambda(LambdaExpression expression)
    {
        var methodInvocation = expression.GetMethodInvocation();
        var invocation = new Moq.Mock<Invocation>();
        invocation.SetupGet(i => i.Method)
            .Returns(methodInvocation.MethodInfo);
        var argumentValues = methodInvocation.Arguments.Values.ToArray();
        invocation.SetupGet(i => i.Arguments)
            .Returns(argumentValues);
        return invocation.Object;
    }
}

internal static class MethodCacheOptionLookupExtensions
{
    internal static TimeSpan? GetRelativeExpiration<T>(this IMethodCacheOptionsLookup subject, Expression<Action<T>> expression)
    {
        var invocation = MockInvocation.Of(expression);
        var cacheOptions = subject.GetCacheEntryOptions(invocation);
        // Only relative expiration currently implemented
        cacheOptions.AbsoluteExpiration.Should().NotHaveValue();
        cacheOptions.SlidingExpiration.Should().NotHaveValue();
        return cacheOptions.AbsoluteExpirationRelativeToNow;
    }
}
