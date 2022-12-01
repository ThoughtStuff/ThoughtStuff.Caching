// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Reflection;

namespace ThoughtStuff.Caching;

public interface IMethodCacheKeyGenerator
{
    string GetCacheKey(MethodInfo methodInfo, object[] arguments);
}