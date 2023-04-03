// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Caching;
using ThoughtStuff.Caching.FileSystem;

// .NET Practice is to place ServiceCollectionExtensions in the following namespace
// to improve discoverability of the extension method during service configuration
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures <see cref="JsonCache"/> as <see cref="ITypedCache"/>
    /// so that it will supersede the default in-memory <see cref="ITypedCache"/>
    /// configured in AddMethodCaching.
    /// Also configures a <see cref="LocalFileCache"/> as <see cref="ITextCache"/>
    /// using default options (attempting to automatically find a local user or appdata directory for the cache)
    /// </summary>
    public static IServiceCollection AddLocalFileTextCache(this IServiceCollection services)
    {
        return services.AddLocalFileTextCache(options =>
            options.BaseDirectory = LocalFileCache.FindLocalCacheBaseDirectory());
    }

    /// <summary>
    /// Configures <see cref="JsonCache"/> as <see cref="ITypedCache"/>
    /// so that it will supersede the default in-memory <see cref="ITypedCache"/>
    /// configured in AddMethodCaching.
    /// Also configures a <see cref="LocalFileCache"/> as <see cref="ITextCache"/>
    /// using the <paramref name="configureLocalFileCacheOptions"/>.
    /// </summary>
    public static IServiceCollection AddLocalFileTextCache(this IServiceCollection services,
                                                           Action<LocalFileCacheOptions> configureLocalFileCacheOptions)
    {
        services.AddTransient<ICacheExpirationService, CacheExpirationService>();
        services.AddTransient<IDefaultCachePolicyService, HardCodedDefaultCachePolicy>();
        services.AddTransient<ITextCache, LocalFileCache>();
        // IMPORTANT: If the serialization format is changed then the cache and any other persisted files will break
        services.AddTransient<ITypedCache, JsonCache>();
        services.AddTransient<IObjectFileSerializer, JsonFileSerializer>();
        services.Configure(configureLocalFileCacheOptions);
        return services;
    }
}
