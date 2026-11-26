# ThoughtStuff.Caching

Injects look-through cache for configured services

The core, Azure, and file-system packages target .NET Standard 2.0 and 2.1. For applications on modern .NET, the 2.x packages support .NET 8 and later. The example and test projects require the .NET 10 SDK.

## Quick Start

```cs
// Cache results returned from MySlowService in Mem Cache
services.AddMethodCaching()
        .AddTransientWithCaching<IMySlowService, MySlowService, MyResult>();

var app = builder.Build();

// Configure method caching policies
var methodCachePolicies = app.Services.GetRequiredService<IMethodCacheOptionsLookup>();
methodCachePolicies.AddRelativeExpiration<IMySlowService>(TimeSpan.FromSeconds(30));
```

## Details

Suppose you have a slow service that fetches information on books by ISBN.

```cs
public interface IBookInfoService
{
    BookInfo GetBookInfo(string isbn);
}

class BookInfoService : IBookInfoService
{
    public BookInfo GetBookInfo(string isbn) { ... }
}
```

The service is slow and book information changes infrequently, so you would like to cache it.
Rather than registering `BookInfoService` with `.AddTransient` you can use `.AddTransientWithCaching`.

```cs
services.AddMethodCaching()
        .AddTransientWithCaching<IBookInfoService, BookInfoService, BookInfo>();
```

The above will register a _caching proxy_ for `IBookInfoService` which will wrap `BookInfoService`.
The first call to get info on a particular book will use `BookInfoService`,
but subsequent calls to get info on the same book will return the cached result from the first call.

```cs
void Example([Inject] IBookInfoService bookInfoService)
{
    var info1 = bookInfoService.GetBookInfo("1234");    // Slow. Saves cache entry using key "GetBookInfo(1234)"
    var info2 = bookInfoService.GetBookInfo("1234");    // Fast. Returns cache entry found using key "GetBookInfo(1234)"
    Assert.Equals(info1, info2);
}
```

## Filesystem cache capacity

You can enable a best-effort size limit when registering the filesystem cache.

```csharp
services.AddLocalFileTextCache(options =>
{
    options.BaseDirectory = cacheDirectory;
    options.SoftSizeLimitBytes = 512L * 1024 * 1024;
});
```

The limit counts published payload and metadata bytes. On a write, the store
tries to make room by evicting expired entries first, then the least recently
used entries. A value can be returned to the caller without being cached.
An existing directory can start over the limit and is trimmed on subsequent
writes.

During downtime, call `TrimToSize` to bring the cache to or below
`SoftSizeLimitBytes`. It inventories the published files and holds the
directory lock while evicting expired entries first, then the least recently
used entries.

```csharp
var cache = (LocalFileCache)serviceProvider.GetRequiredService<ITextCache>();
cache.TrimToSize(TimeSpan.FromMinutes(15), cancellationToken);
```

The timeout includes waiting for the lock and scanning the cache. The trim
checks the time budget and cancellation between units of work, so an
in-progress filesystem or SQLite operation can run past the deadline.
Timeout throws `TimeoutException`, caller cancellation throws
`OperationCanceledException`, and storage failures are surfaced immediately.
Completed evictions remain in effect.

When enabled the store tracks access times in a SQLite catalog.
The catalog is built from existing files so the limit can be applied to an existing cache directory.

Use one process per cache directory when limiting is enabled.
Instances in that process share accounting and access tracking. The package
logs a warning through .NET logging if it detects another process.

## Current Limitations

### Single Return Type per Interface

Only one return type can be cached per service interface.
This works well for services that implement a Single Responsibility, but not for larger classes.
Often times simplified interface(s) can be created.
The simplified interfaces may only have 1 function each and all be implemented by the large class.

### Transient Lifetime

Only the `Transient` lifetime is implemented.

### Cache Key uses Interface Name + Method Name + Arguments

A cache key is automatically generated from the method invocation based on the method name and passed arguments.
For example: `"IMyBookService.GetBookInfo('The Great Gatsby')"`.
Other environment information is not included.

Be careful in multi-user or multi-tenant environments not to cache user-specific or tenant-specific results.
For example, caching a method like `GetCurrentUserFavoriteColor()` would end up returning the same color for all users.
To make it amenable to method caching change it to `GetUserFavoriteColor(userId)`.

---

> The programmer, like the poet, works only slightly removed from pure **thought-stuff**.
> He builds his castles in the air, from air, creating by exertion of the imagination.
> Few media of creation are so flexible, so easy to polish and rework,
> so readily capable of realizing grand conceptual structures.

&mdash; Fred P. Brooks
