// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using System;
using ThoughtStuff.Core.Abstractions;

// .NET Practice is to place ServiceCollectionExtensions in the following namespace
// to improve discoverability of the extension method during service configuration
namespace ThoughtStuff.Caching;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Castle Dynamic Proxy based caching requirements.
    /// Required for using <see cref="AddTransientWithCaching{TService, TImplementation, TResult}(IServiceCollection)"/>
    /// </summary>
    /// <param name="services"></param>
    public static IServiceCollection AddMethodCaching(this IServiceCollection services)
    {
        services.AddTransient<IObjectFileSerializer, JsonFileSerializer>(); // IMPORTANT: If the serialization format is changed then the cache and any other persisted files will break
        services.AddLocalFileTextCache();
        services.AddTransient<ITypedCache, JsonCache>();
        services.AddTransient<IMethodCacheKeyGenerator, MethodCacheKeyGenerator>();
        // MethodCacheOptionsLookup must be added as singleton because the mappings are stored in member variables
        services.AddSingleton<IMethodCacheOptionsLookup, MethodCacheOptionsLookup>();
        // TODO: Document why ProxyGenerator added as singleton w/ link to Castle docs
        services.AddSingleton<IProxyGenerator, ProxyGenerator>();
        return services;
    }

    /// <summary>
    /// Configures a <see cref="LocalFileCache"/> as <see cref="ITextCache"/>
    /// using default options (attempting to automatically find a local user or appdata directory for the cache)
    /// </summary>
    public static IServiceCollection AddLocalFileTextCache(this IServiceCollection services)
    {
        return services.AddLocalFileTextCache(options =>
            options.BaseDirectory = LocalFileCache.FindLocalCacheBaseDirectory());
    }

    /// <summary>
    /// Configures a <see cref="LocalFileCache"/> as <see cref="ITextCache"/>
    /// </summary>
    public static IServiceCollection AddLocalFileTextCache(this IServiceCollection services,
                                                           Action<LocalFileCacheOptions> configureLocalFileCacheOptions)
    {
        services.AddTransient<ICacheExpirationService, CacheExpirationService>();
        services.AddTransient<IDefaultCachePolicyService, HardCodedDefaultCachePolicy>();
        services.AddTransient<ITextCache, LocalFileCache>();
        services.Configure(configureLocalFileCacheOptions);
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
