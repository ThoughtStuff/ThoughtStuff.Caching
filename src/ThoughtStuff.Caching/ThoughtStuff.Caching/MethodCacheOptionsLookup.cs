// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ThoughtStuff.Caching
{
    public class MethodCacheOptionsLookup : IMethodCacheOptionsLookup
    {
        private readonly Dictionary<Type, DistributedCacheEntryOptions> defaultsByType = new();
        private readonly ConcurrentDictionary<Type, List<MethodInvocationMatcher>> matcherMap = new();
        private readonly ILogger<IMethodCacheOptionsLookup> logger;

        public MethodCacheOptionsLookup(ILogger<IMethodCacheOptionsLookup> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <inheritdoc/>
        public DistributedCacheEntryOptions GetCacheEntryOptions(IInvocation invocation)
        {
            var methodInfo = invocation.Method;
            var declaringType = invocation.Method.DeclaringType;
            if (matcherMap.TryGetValue(declaringType, out var invocationMatchers))
            {
                foreach (var invocationMatcher in invocationMatchers)
                {
                    if (invocationMatcher.Matches(invocation))
                        return invocationMatcher.CacheOptions;
                }
            }
            return GetByDeclaringType(methodInfo);
        }

        private DistributedCacheEntryOptions GetByDeclaringType(MethodInfo methodInfo)
        {
            var key = methodInfo.DeclaringType;
            if (defaultsByType.TryGetValue(key, out var cacheEntryOptions))
                return cacheEntryOptions;
            logger.LogWarning($"Missing cache entry options configuration for {methodInfo.DeclaringType.Name} {methodInfo}.");
            return new DistributedCacheEntryOptions();
        }

        /// <inheritdoc/>
        public MethodCacheOptionsLookup AddRelativeExpiration<T>(TimeSpan relativeExpiration)
        {
            var key = typeof(T);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = relativeExpiration
            };
            defaultsByType.Add(key, cacheOptions);
            return this;
        }

        /// <inheritdoc/>
        public MethodCacheOptionsLookup AddRelativeExpiration<T>(TimeSpan relativeExpiration, Expression<Action<T>> expression)
        {
            var methodInvocation = expression.GetMethodInvocation();
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = relativeExpiration
            };
            var methodMatcher = new MethodInvocationMatcher(methodInvocation, cacheOptions);
            var declaringType = methodInvocation.MethodInfo.DeclaringType;
            var invocationMatchers = matcherMap.GetOrAdd(declaringType, new List<MethodInvocationMatcher>());
            invocationMatchers.Add(methodMatcher);
            return this;
        }
    }
}