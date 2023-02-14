// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Reflection;

namespace ThoughtStuff.Caching;

public interface IMethodCacheKeyGenerator
{
    /// <summary>
    /// Returns a Key suitable to uniquely identify this particular method invocation.
    /// For example: "IMyBookService.GetBookInfo('The Great Gatsby')"
    /// </summary>
    /// <param name="methodInfo">The method being invoked</param>
    /// <param name="arguments">The arguments being passed in this invocation</param>
    /// <returns>A cache key for this method invocation</returns>
    string GetCacheKey(MethodInfo methodInfo, object[] arguments);
}
