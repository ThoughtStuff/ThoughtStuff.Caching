// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

// .NET Practice is to place ServiceCollectionExtensions in the following namespace
// to improve discoverability of the extension method during service configuration
namespace ThoughtStuff.Caching.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCachingWithAzureBlobs(this IServiceCollection services,
                                                              Action<AzureCachingOptions> configureAzureCachingOptions)
    {
        AddCachingWithAzureBlobs(services);
        return services.Configure(configureAzureCachingOptions);
    }

    public static IServiceCollection AddCachingWithAzureBlobs(this IServiceCollection services,
                                                              IConfiguration configuration)
    {
        AddCachingWithAzureBlobs(services);
        return services.Configure<AzureCachingOptions>(configuration.GetSection(AzureCachingOptions.Name));
    }

    private static void AddCachingWithAzureBlobs(IServiceCollection services)
    {
        services.AddTransient<IBlobStorageService, BlobStorageService>();
        services.AddTransient<IObjectDictionaryConverter, ObjectDictionaryConverter>();
        services.AddTransient<ITextCache, AzureBlobTextCache>();
        services.AddTransient<IZipArchiveBlobStorage, ZipArchiveBlobStorage>();
    }
}
