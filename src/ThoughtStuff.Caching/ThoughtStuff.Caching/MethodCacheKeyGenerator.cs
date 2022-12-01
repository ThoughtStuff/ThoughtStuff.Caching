// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace ThoughtStuff.Caching
{
    public class MethodCacheKeyGenerator : IMethodCacheKeyGenerator
    {
        public string GetCacheKey(MethodInfo methodInfo, object[] arguments)
        {
            var methodName = methodInfo.Name;
            var argStrings = new List<string>(arguments.Length);
            foreach (var argument in arguments)
            {
                if (argument is null)
                    argStrings.Add("(null)");
                else if (argument is string || argument is Uri)
                    // TODO: For URI's I want to remove the https:// scheme
                    argStrings.Add($"'{argument}'");
                else if (argument is IProgress<double>)
                    argStrings.Add("progress");
                else
                {
                    var fullTypeName = argument.GetType().FullName;
                    var argumentString = argument.ToString();
                    if (argumentString == fullTypeName)
                        throw new ArgumentException($"The type '{fullTypeName}' must override `ToString()` for Method Cache Key generation.");
                    argStrings.Add(argumentString);
                }
            }
            var argList = string.Join(",", argStrings);
            return $"{methodName}({argList})";
        }
    }
}
