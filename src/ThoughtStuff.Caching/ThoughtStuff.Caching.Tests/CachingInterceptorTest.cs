// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
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

        private int _calls;
        public int Calls { get => _calls; }

        public bool VoidMethodCalled { get; private set; }

        public virtual int BlockingOperation()
        {
            Interlocked.Increment(ref _calls);
            // Insert a delay to simulate an operation
            Thread.Sleep(50);
            return operationResult;
        }

        public virtual async Task<int> AsyncOperation()
        {
            Interlocked.Increment(ref _calls);
            // Insert a delay to simulate an async operation
            await Task.Delay(50);
            return operationResult;
        }

        public virtual int ThrowingOperation()
        {
            Interlocked.Increment(ref _calls);
            // Insert a delay to simulate an operation
            Thread.Sleep(50);
            throw new Exception("Expected exception");
        }

        public virtual async Task<int> ThrowingOperationAsync()
        {
            Interlocked.Increment(ref _calls);
            // Insert a delay to simulate an async operation
            await Task.Delay(50);
            throw new Exception("Expected exception");
        }

        public virtual void VoidMethod() => VoidMethodCalled = true;

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
        var cacheLockProvider = new CacheLockProvider();
        var cachingInterceptor = new CachingInterceptor<int>(methodCacheKeyGenerator, methodCacheOptionsLookup, cache, cacheLockProvider);
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
                                       JsonTypedCache cache)
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
                                              JsonTypedCache cache)
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

    [Theory(DisplayName = "Caching: Ignore Void Returning Invocations"), AutoMoq]
    public void VoidReturn([Frozen] Mock<ITypedCache> cache)
    {
        cache.Setup(c => c.Contains(It.IsAny<string>())).Returns(false);
        var service = MakeProxyService(cache.Object);

        service.VoidMethod();

        //cache.Verify(c => c.Set(It.IsAny<string>(), 42, It.IsAny<DistributedCacheEntryOptions>()));
        service.VoidMethodCalled.Should().BeTrue();
    }

    [Theory(DisplayName = "Caching: Concurrent Sync: Prevent multiple threads calling underlying operation"), CacheTest]
    public void ConcurrentSync(int expected,
                               JsonTypedCache cache)
    {
        const int count = 10;
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);

        var results = new int[count];
        var calls = Enumerable.Range(0, count)
                              .Select(i => new Action(() => results[i] = service.BlockingOperation()))
                              .ToArray();
        Parallel.Invoke(calls);
        results.Should().OnlyContain(x => x == expected);
        service.Calls.Should().Be(1);
    }

    [Theory(DisplayName = "Caching: Concurrent Async: Prevent multiple threads calling underlying async operation"), CacheTest]
    public async Task ConcurrentAsync(int expected,
                                      JsonTypedCache cache)
    {
        const int count = 10;
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);

        var results = new int[count];
        var calls = Enumerable.Range(0, count)
                              .Select(i => Task.Run(async () => results[i] = await service.AsyncOperation()))
                              .ToArray();
        await Task.WhenAll(calls);
        results.Should().OnlyContain(x => x == expected);
        service.Calls.Should().Be(1);
    }

    [Theory(DisplayName = "Caching: Concurrent Sync: Avoids deadlock when underlying operation throws"), CacheTest]
    public void ConcurrentSyncThrowing(int expected,
                                       JsonTypedCache cache)
    {
        const int count = 5;
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);

        var results = new int[count];
        var calls = Enumerable.Range(0, count)
                              .Select(i => new Action(() => results[i] = service.ThrowingOperation()))
                              .ToArray();
        var testTask = Task.Run(() =>
        {
            try
            {
                Parallel.Invoke(calls);
            }
            catch (Exception)
            {
            }
        });
        var completedInTime = testTask.Wait(2000);
        completedInTime.Should().BeTrue();
        service.Calls.Should().Be(count);
    }

    [Theory(DisplayName = "Caching: Concurrent Async: Avoids deadlock when underlying operation throws"), CacheTest]
    public void ConcurrentAsyncThrowing(int expected,
                                       JsonTypedCache cache)
    {
        const int count = 5;
        var service = MakeProxyService(cache);
        service.SetOperationResult(expected);

        var results = new int[count];
        var calls = Enumerable.Range(0, count)
                              .Select(i => Task.Run(async () => results[i] = await service.ThrowingOperationAsync()))
                              .ToArray();
        bool? completedInTime = null;
        try
        {
            var testTask = Task.WhenAll(calls);
            completedInTime = testTask.Wait(2000);
        }
        catch (AggregateException)
        {
        }
        completedInTime.Should().NotBeFalse();
        service.Calls.Should().Be(count);
    }

    [Theory(DisplayName = "Caching: Avoids deadlock when 2nd Contains check fails"), CacheTest]
    public void SecondContainsFails(Mock<ITypedCache> cache)
    {
        const int count = 5;
        // This sequence makes it likely that the 2nd check for cache.Contains
        // (after the lock has been obtained)
        // will throw for one of the invocations
        cache.SetupSequence(x => x.Contains(It.IsAny<string>()))
             .Returns(false)
             .Returns(false)
             .Returns(false)
             .Returns(false)
             .Returns(false)
             .Throws<InvalidOperationException>()
             .Throws<InvalidOperationException>()
             .Throws<InvalidOperationException>()
             .Throws<InvalidOperationException>()
             .Throws<InvalidOperationException>();
        var service = MakeProxyService(cache.Object);

        var results = new int[count];
        var calls = Enumerable.Range(0, count)
                              .Select(i => new Action(() => results[i] = service.BlockingOperation()))
                              .ToArray();
        var testTask = Task.Run(() =>
        {
            try
            {
                Parallel.Invoke(calls);
            }
            catch (AggregateException aggregate)
            {
                // The exception should be propagated
                aggregate.InnerExceptions.Should().AllBeOfType<InvalidOperationException>();
            }
        });
        // The exception should not cause a deadlock
        var completedInTime = testTask.Wait(2000);
        completedInTime.Should().BeTrue();
    }
}
