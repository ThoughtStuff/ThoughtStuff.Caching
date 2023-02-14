// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class MethodCacheKeyGeneratorTest
{
    public interface IExampleService
    {
        public int Example(string arg1, Uri uri, ExampleDto a, ExampleDto b);
    }

    public class ExampleDto
    {
        public string? Key { get; set; }

        public override string ToString() => $"Key: '{Key}'";
    }

    [Fact(DisplayName = "Caching: Method Name Key")]
    public void CacheKey()
    {
        var subject = new MethodCacheKeyGenerator();
        var methodInfo = typeof(IExampleService).GetMethod(nameof(IExampleService.Example))!;
        var arguments = new object?[]
        {
            "xyz",
            new Uri("https://example.com/"),
            null,
            new ExampleDto { Key = "gamma" }
        };

        var key = subject.GetCacheKey(methodInfo, arguments);

        key.Should().Be("IExampleService.Example('xyz','https://example.com/',(null),Key: 'gamma')");
    }
}
