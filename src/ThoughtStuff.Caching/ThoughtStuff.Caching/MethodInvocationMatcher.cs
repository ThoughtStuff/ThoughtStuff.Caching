// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Linq;

namespace ThoughtStuff.Caching
{
    class MethodInvocationMatcher
    {
        public MethodInvocation MethodInvocation { get; }
        public DistributedCacheEntryOptions CacheOptions { get; }

        public MethodInvocationMatcher(MethodInvocation methodInvocation, DistributedCacheEntryOptions cacheOptions)
        {
            MethodInvocation = methodInvocation ?? throw new ArgumentNullException(nameof(methodInvocation));
            CacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
        }

        public bool Matches(IInvocation invocation)
        {
            if (invocation.Method != MethodInvocation.MethodInfo)
                return false;
            var parameterNames = invocation.Method.GetParameters().Select(p => p.Name).ToArray();
            var arguments = MethodInvocation.Arguments;
            if (!parameterNames.SequenceEqual(arguments.Keys))
                return false;
            for (int i = 0; i < invocation.Arguments.Length; i++)
            {
                var parameterName = parameterNames[i];
                var expectedValue = arguments[parameterName];
                if (expectedValue.Equals(AnyArgument.Placeholder))
                    continue;
                var argumentValue = invocation.Arguments[i];
                if (!expectedValue.Equals(argumentValue))
                    return false;
            }
            return true;
        }
    }
}