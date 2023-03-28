// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ThoughtStuff.Caching.FileSystem;
// Moq also has an IInvocation, so disambiguate:
using Invocation = Castle.DynamicProxy.IInvocation;

namespace ThoughtStuff.Caching.Tests;

public class CachingInterceptorTest
{
    [Theory(DisplayName = "Caching: Interceptor Proceed"), AutoMoq]
    public void BaseProceed([Frozen] Mock<ITypedCache> cache,
                            CachingInterceptor<object> cachingInterceptor,
                            Mock<Invocation> invocation)
    {
        cache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);

        cachingInterceptor.Intercept(invocation.Object);

        invocation.Verify(i => i.Proceed());
    }

    public class TestService
    {
        private int operationResult = 42;
        public int Calls { get; private set; }

        public virtual int BlockingOperation()
        {
            ++Calls;
            return operationResult;
        }

        public virtual async Task<int> AsyncOperation()
        {
            ++Calls;
            // Insert a delay to simulate an async operation
            await Task.Delay(50);
            return operationResult;
        }

        public void SetOperationResult(int count)
        {
            operationResult = count;
        }
    }

    private static TestService MakeProxyService(ITypedCache cache)
    {
        // TODO: Setup caching service config method (to help w/ testing and app service config)
        var methodCacheKeyGenerator = new MethodCacheKeyGenerator();
        var logger = Mock.Of<ILogger<IMethodCacheOptionsLookup>>();
        var methodCacheOptionsLookup = new MethodCacheOptionsLookup(logger);
        var cachingInterceptor = new CachingInterceptor<int>(methodCacheKeyGenerator, methodCacheOptionsLookup, cache);
        var proxyGenerator = new ProxyGenerator();
        var proxyService = proxyGenerator.CreateClassProxy<TestService>(cachingInterceptor);
        return proxyService;
    }

    [Theory(DisplayName = "Caching: Interceptor Cache Sync Result"), AutoMoq]
    public void CacheSyncResult([Frozen] Mock<ITypedCache> cache)
    {
        cache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
        var service = MakeProxyService(cache.Object);

        var result = service.BlockingOperation();

        result.Should().Be(42);
        cache.Verify(c => c.Set("TestService.BlockingOperation()", 42, It.IsAny<DistributedCacheEntryOptions>()));
        service.Calls.Should().Be(1);
    }

    [Theory(DisplayName = "Caching: Interceptor Sync Return Cached Result"), CacheTest]
    public void ReturnCachedSyncResult(int expected,
                                       int notExpected,
                                       JsonCache cache)
    {
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);
        var result = service.BlockingOperation();
        result.Should().Be(expected);

        // Change the value returned by the service
        service.SetOperationResult(notExpected);

        // Verify the value returned with the interceptor has not been changed (thus service not called again)
        var result2 = service.BlockingOperation();
        result2.Should().Be(expected);
        cache.Get<int>("TestService.BlockingOperation()").Should().Be(expected);
        service.Calls.Should().Be(1);
    }

    [Theory(DisplayName = "Caching: Interceptor Cache Async Result"), AutoMoq]
    public async Task CacheAsyncResult([Frozen] Mock<ITypedCache> cache)
    {
        cache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
        var service = MakeProxyService(cache.Object);

        var result = await service.AsyncOperation();

        result.Should().Be(42);
        service.Calls.Should().Be(1);

        // Allow the task continuation to run which sets the cache value
        await Task.Delay(100);
        cache.Verify(c => c.Set("TestService.AsyncOperation()", 42, It.IsAny<DistributedCacheEntryOptions>()));
    }

    [Theory(DisplayName = "Caching: Interceptor Async Return Cached Result"), CacheTest]
    public async Task ReturnCachedAsyncResult(int expected,
                                              int notExpected,
                                              JsonCache cache)
    {
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);
        var result = await service.AsyncOperation();
        result.Should().Be(expected);

        // Allow the task continuation to run which sets the cache value
        await Task.Delay(100);
        cache.Get<int>("TestService.AsyncOperation()").Should().Be(expected);

        // Change the value returned by the service
        service.SetOperationResult(notExpected);

        // Verify the value returned with the interceptor has not been changed (thus service not called again)
        var result2 = await service.AsyncOperation();
        result2.Should().Be(expected);
        service.Calls.Should().Be(1);
    }
}
