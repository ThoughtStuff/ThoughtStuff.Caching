using Castle.DynamicProxy;
using ThoughtStuff.Caching.Core;

namespace ThoughtStuff.Caching;

public class CachingInterceptor<T> : IInterceptor
{
    private readonly IMethodCacheKeyGenerator methodCacheKeyGenerator;
    private readonly IMethodCacheOptionsLookup methodCacheOptionsLookup;
    private readonly ITypedCache cache;
    private readonly ICacheLockProvider cacheLockProvider;

    public CachingInterceptor(IMethodCacheKeyGenerator methodCacheKeyGenerator,
                              IMethodCacheOptionsLookup methodCacheOptionsLookup,
                              ITypedCache cache,
                              ICacheLockProvider cacheLockProvider)
    {
        this.methodCacheKeyGenerator = methodCacheKeyGenerator ?? throw new ArgumentNullException(nameof(methodCacheKeyGenerator));
        this.methodCacheOptionsLookup = methodCacheOptionsLookup ?? throw new ArgumentNullException(nameof(methodCacheOptionsLookup));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.cacheLockProvider = cacheLockProvider ?? throw new ArgumentNullException(nameof(cacheLockProvider));
    }

    public void Intercept(IInvocation invocation)
    {
        var returnType = invocation.Method.ReturnType;
        // If method has void return type, it cannot be cached, so pass it through and do nothing
        if (returnType == typeof(void))
        {
            // TODO: if invocation == set_Prop then cache.remove(get_Prop). Property setters return void.
            invocation.Proceed();
            return;
        }

        var isAsync = typeof(Task).IsAssignableFrom(returnType);
        var expectedType = isAsync ? typeof(Task<T>) : typeof(T);

        // The return type does not match the Type T setup for the caching interceptor
        if (returnType != expectedType)
        {
            throw new Exception($"Invalid service configuration for caching. The return type of the method {returnType} does not match the configured type {expectedType}");
            // TODO: Perhaps a better way is needed to add the caching interceptor for multiple return types per service?
        }

        // Castle's array may contain nulls, which the built-in key generator handles.
        var cacheKey = methodCacheKeyGenerator.GetCacheKey(invocation.Method, (object[])invocation.Arguments);
        if (TryGetResultFromCache(invocation, isAsync, cacheKey))
            return;

        // Prevent multiple attempts to call underlying implementation
        // by using a lock based on the cache key
        var cacheLock = cacheLockProvider.GetCacheLockObject(cacheKey);
        cacheLock.Wait();
        var releaseLock = true;
        try
        {
            // Another caller may have filled the cache while we waited.
            if (TryGetResultFromCache(invocation, isAsync, cacheKey))
                return;

            invocation.Proceed();
            if (isAsync)
            {
                var workTask = (Task<T>)invocation.ReturnValue!;
                var cachedTask = Task.Run(async () =>
                {
                    try
                    {
                        var result = await workTask;
                        SetResultInCache(invocation, cacheKey, result);
                        return result;
                    }
                    finally
                    {
                        cacheLock.Release();
                    }
                });
                releaseLock = false;
                invocation.ReturnValue = cachedTask;
            }
            else
            {
                SetResultInCache(invocation, cacheKey, (T)invocation.ReturnValue!);
            }
        }
        finally
        {
            if (releaseLock)
                cacheLock.Release();
        }
    }

    private bool TryGetResultFromCache(IInvocation invocation, bool isAsync, string cacheKey)
    {
        if (!cache.Contains(cacheKey))
            return false;

        var jsonCache = cache as JsonTypedCache;
        T? result;
        try
        {
            if (jsonCache is not null)
            {
                if (!jsonCache.TryGet(cacheKey, out result))
                    return false;
            }
            else
            {
                result = cache.Get<T>(cacheKey);
            }
        }
        catch (Exception cacheException)
        {
            var cacheLocation = cache.GetLocation(cacheKey);
            throw new Exception($"Failure fetching cache entry at cache location: '{cacheLocation}'", cacheException);
        }
        if (result.IsDefault())
        {
            // Other typed caches report a miss by returning default.
            if (jsonCache is null && !cache.Contains(cacheKey))
                return false;
            throw new InvalidOperationException($"Key '{cacheKey}' was present in cache '{cache}', but value was default.");
        }
        if (isAsync)
            invocation.ReturnValue = Task.FromResult(result);
        else
            invocation.ReturnValue = result;
        return true;
    }

    private void SetResultInCache(IInvocation invocation, string cacheKey, T result)
    {
        var cacheOptions = methodCacheOptionsLookup.GetCacheEntryOptions(invocation);
        cache.Set(cacheKey, result, cacheOptions);
    }
}
