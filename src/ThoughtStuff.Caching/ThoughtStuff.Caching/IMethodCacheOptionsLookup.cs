// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using System.Linq.Expressions;

namespace ThoughtStuff.Caching;

public interface IMethodCacheOptionsLookup
{
    /// <summary>
    /// Retrieve the configured caching options / policies for the given <paramref name="invocation"/>.
    /// </summary>
    DistributedCacheEntryOptions GetCacheEntryOptions(IInvocation invocation);

    /// <summary>
    /// Configures default relative expiration duration for any method of the given declaring Type 
    /// <typeparamref name="T"/>.
    /// <para></para>
    /// See <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/>
    /// </summary>
    MethodCacheOptionsLookup AddRelativeExpiration<T>(TimeSpan relativeExpiration);

    /// <summary>
    /// Configures relative expiration duration for a specific method invocation.
    /// Properties of <see cref="AnyArgument"/> can be used as wildcard arguments.
    /// <para></para>
    /// See <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/>
    /// </summary>
    MethodCacheOptionsLookup AddRelativeExpiration<T>(TimeSpan relativeExpiration, Expression<Action<T>> expression);
}
