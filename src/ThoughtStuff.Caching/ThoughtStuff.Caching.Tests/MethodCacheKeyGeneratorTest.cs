// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class MethodCacheKeyGeneratorTest
{
    public class ExampleDto
    {
        public string? Key { get; set; }

        public override string ToString() => $"Key: '{Key}'";
    }

    public int Example(string arg1, Uri uri, ExampleDto a, ExampleDto b) => 0;

    [Fact(DisplayName = "Caching: Method Name Key")]
    public void CacheKey()
    {
        var subject = new MethodCacheKeyGenerator();
        var methodInfo = GetType().GetMethod(nameof(Example))!;
        var arguments = new object?[]
        {
            "xyz",
            new Uri("https://example.com/"),
            null,
            new ExampleDto { Key = "gamma" }
        };

        var key = subject.GetCacheKey(methodInfo, arguments);

        key.Should().Be("Example('xyz','https://example.com/',(null),Key: 'gamma')");
    }
}
