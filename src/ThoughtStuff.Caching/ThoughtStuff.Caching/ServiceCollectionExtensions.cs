// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThoughtStuff.Caching;

// .NET Practice is to place ServiceCollectionExtensions in the following namespace
// to improve discoverability of the extension method during service configuration
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds support for inserting caching of service method calls.
    /// <para/>
    /// Required for using <see cref="AddTransientWithCaching{TService, TImplementation, TResult}(IServiceCollection)"/>
    /// <para/>
    /// By default the cache will use <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>
    /// unless another implementation of <see cref="ITypedCache"/> is provided.
    /// </summary>
    public static IServiceCollection AddMethodCaching(this IServiceCollection services)
    {
        // Default Cache is MemotyCache
        // Use `TryAdd` so as not to replace an ITypedCache that was previously configured
        services.TryAddTransient<ITypedCache, MemoryCacheTypedCache>();

        // Register Cache Management service accessors
        // - IManagedCache is cast from configured ITypedCache
        services.AddTransient(sp =>
        {
            var cache = sp.GetRequiredService<ITypedCache>();
            if (cache is IManagedCache managedCache)
            {
                return managedCache;
            }
            throw new InvalidOperationException($"The configured {nameof(ITypedCache)} '{cache.GetType().Name}' does not implement {nameof(IManagedCache)}.");
        });
        // - ICacheManager is fetched from IManagedCache
        services.AddTransient(sp =>
        {
            var managedCache = sp.GetRequiredService<IManagedCache>();
            return managedCache.GetCacheManager();
        });

        // Method Caching
        services.AddTransient<IMethodCacheKeyGenerator, MethodCacheKeyGenerator>();
        // MethodCacheOptionsLookup must be added as singleton because the mappings are stored in member variables
        services.AddSingleton<IMethodCacheOptionsLookup, MethodCacheOptionsLookup>();
        // TODO: Document why ProxyGenerator added as singleton w/ link to Castle docs
        services.AddSingleton<IProxyGenerator, ProxyGenerator>();
        services.AddTransient<ICacheExpirationService, CacheExpirationService>();
        // Default cache policy can be replaced
        services.TryAddTransient<IDefaultCachePolicyService, HardCodedDefaultCachePolicy>();
        return services;
    }

    /// <summary>
    /// Adds a Transient Service of the type specified in <typeparamref name="TService"/>
    /// with the implementation specified in <typeparamref name="TImplementation"/>
    /// and uses a <see cref="IProxyGenerator"/> to inject a caching <see cref="IInterceptor"/>
    /// for caching of <typeparamref name="TResult"/>
    /// </summary>
    public static IServiceCollection AddTransientWithCaching<TService, TImplementation, TResult>(this IServiceCollection services)
        where TService : class where TImplementation : class, TService
    {
        // Register the concrete target implementation, which we will need below
        services.AddTransient<TImplementation>();
        // Register the caching interceptor
        // It does not have to be singleton w/ the JSON File implementation, but simpler implementations would need to be
        services.AddTransient<CachingInterceptor<TResult>>();
        services.AddTransient(sp =>
        {
            // Use the Castle ProxyGenerator to inject our caching interceptor proxy
            // See https://crosscuttingconcerns.com/Caching-example-with-DynamicProxy
            var proxyGenerator = sp.GetRequiredService<IProxyGenerator>();
            var cachingInterceptor = sp.GetRequiredService<CachingInterceptor<TResult>>();
            var targetImplementation = sp.GetRequiredService<TImplementation>();
            return proxyGenerator.CreateInterfaceProxyWithTarget<TService>(targetImplementation, cachingInterceptor);
        });
        return services;
    }
}
