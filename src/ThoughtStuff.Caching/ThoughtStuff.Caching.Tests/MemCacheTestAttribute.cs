// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.AutoMoq;
using AutoFixture;
using Microsoft.Extensions.Caching.Memory;

namespace ThoughtStuff.Caching.Tests;

/// <summary>
/// Registers <see cref="IMemoryCache"/>, <see cref="MemoryCacheTypedCache"/>
/// and <see cref="AutoMoqCustomization"/>
/// </summary>
public class MemCacheTestAttribute : AutoDataAttribute
{
    public MemCacheTestAttribute() : base(BuildFixture)
    {        
    }

    internal static IFixture BuildFixture()
    {
        var fixture = new Fixture();
        var memCacheOptions = new MemoryCacheOptions();
        fixture.Register<IMemoryCache>(() => new MemoryCache(memCacheOptions));
        fixture.Register<IMemoryCache, MemoryCacheTypedCache>(mc => new MemoryCacheTypedCache(mc));
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true });
        return fixture;
    }
}
