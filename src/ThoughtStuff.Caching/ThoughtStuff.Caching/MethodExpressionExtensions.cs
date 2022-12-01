// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ThoughtStuff.Caching;

public static class MethodExpressionExtensions
{
    public static MethodInvocation GetMethodInvocation<T>(this Expression<Action<T>> expression)
        => (expression as LambdaExpression).GetMethodInvocation();

    public static MethodInvocation GetMethodInvocation(this LambdaExpression expression)
    {
        var methodCall = (MethodCallExpression)expression.Body;
        var methodInfo = methodCall.Method;
        var parameterNames = methodInfo
            .GetParameters()
            .Select(p => p.Name);
        var argumentValues = GetMethodArgumentValues(methodCall);
        var arguments = parameterNames
            .Zip(argumentValues, (parameterName, argumentValue) => (parameterName, argumentValue))
            .ToDictionary(x => x.parameterName, x => x.argumentValue);
        return new MethodInvocation(methodInfo, arguments);
    }

    private static IEnumerable<object> GetMethodArgumentValues(MethodCallExpression methodCall)
    {
        foreach (var expression in methodCall.Arguments)
        {
            switch (expression)
            {
                case MemberExpression memberExpression:
                    if (memberExpression.Member.DeclaringType == typeof(AnyArgument))
                        yield return AnyArgument.Placeholder;
                    else
                        yield return Expression.Lambda(memberExpression).Compile().DynamicInvoke();
                    break;
                case ConstantExpression constantExpression:
                    yield return constantExpression.Value;
                    break;
                default:
                    throw new NotImplementedException($"Not implemented: method expression involving non constants: '{expression}'.");
            }
        }
    }
}