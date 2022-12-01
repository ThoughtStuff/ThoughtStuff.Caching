// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using System;
using System.Threading.Tasks;
using ThoughtStuff.Core;

namespace ThoughtStuff.Caching;

public class CachingInterceptor<T> : IInterceptor
{
    private readonly IMethodCacheKeyGenerator methodCacheKeyGenerator;
    private readonly IMethodCacheOptionsLookup methodCacheOptionsLookup;
    private readonly ITypedCache cache;

    public CachingInterceptor(IMethodCacheKeyGenerator methodCacheKeyGenerator,
                              IMethodCacheOptionsLookup methodCacheOptionsLookup,
                              ITypedCache cache)
    {
        this.methodCacheKeyGenerator = methodCacheKeyGenerator ?? throw new ArgumentNullException(nameof(methodCacheKeyGenerator));
        this.methodCacheOptionsLookup = methodCacheOptionsLookup ?? throw new ArgumentNullException(nameof(methodCacheOptionsLookup));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public void Intercept(IInvocation invocation)
    {
        var returnType = invocation.Method.ReturnType;
        // TODO: If method has no return type it was misconfigured
        var isAsync = typeof(Task).IsAssignableFrom(returnType);
        var expectedType = isAsync ? typeof(Task<T>) : typeof(T);

        // The return type does not match the Type T setup for the caching interceptor
        if (returnType != expectedType)
        {
            throw new Exception($"Invalid service configuration for caching. The return type of the method {returnType} does not match the configured type {expectedType}");
            // TODO: Perhaps a better way is needed to add the caching interceptor for multiple return types per service?
        }

        var cacheKey = methodCacheKeyGenerator.GetCacheKey(invocation.Method, invocation.Arguments);
        if (cache.Contains(cacheKey))
        {
            T result;
            try
            {
                result = cache.Get<T>(cacheKey);
            }
            catch (Exception cacheException)
            {
                var cacheLocation = cache.GetLocation(cacheKey);
                throw new Exception($"Failure fetching cache entry at cache location: '{cacheLocation}'", cacheException);
            }
            if (result.IsDefault())
                throw new InvalidOperationException($"Key '{cacheKey}' was present in cache '{cache}', but value was default.");
            if (isAsync)
                invocation.ReturnValue = Task.FromResult(result);
            else
                invocation.ReturnValue = result;
            return;
        }

        invocation.Proceed();

        if (isAsync)
        {
            var task = (Task<T>)invocation.ReturnValue;
            task.ConfigureAwait(false);
            // TODO: Prevent process from exiting before this continuation gets run
            // https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice#registering-hosted-services-in-your-webhost-or-host
            task.ContinueWith(t => SetResultInCache(invocation, cacheKey, t));
        }
        else
        {
            SetResultInCache(invocation, cacheKey, (T)invocation.ReturnValue);
        }
    }

    private void SetResultInCache(IInvocation invocation, string cacheKey, Task<T> task)
    {
        if (task.Status != TaskStatus.RanToCompletion)
            return;
        T result = task.Result;
        SetResultInCache(invocation, cacheKey, result);
    }

    private void SetResultInCache(IInvocation invocation, string cacheKey, T result)
    {
        var cacheOptions = methodCacheOptionsLookup.GetCacheEntryOptions(invocation);
        cache.Set(cacheKey, result, cacheOptions);
    }
}
