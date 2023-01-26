// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using System.Reflection;

namespace ThoughtStuff.Caching;

public class MethodInvocation
{
    public MethodInfo MethodInfo { get; }
    public IReadOnlyDictionary<string, object> Arguments { get; }

    public MethodInvocation(MethodInfo methodInfo, IReadOnlyDictionary<string, object> arguments)
    {
        MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }
}
