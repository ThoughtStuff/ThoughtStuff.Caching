# ThoughtStuff.Caching

Injects look-through cache for configured services

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

## Current Limitations

### Single Return Type per Interface

Only one return type can be cached per service interface. 
This works well for services that implement a Single Responsibility, but not for larger classes. 
Often times simplified interface(s) can be created. 
The simplified interfaces may only have 1 function each and all be implemented by the large class.

### Transient Lifetime

Only the `Transient` lifetime is implemented.


---

> The programmer, like the poet, works only slightly removed from pure **thought-stuff**. 
> He builds his castles in the air, from air, creating by exertion of the imagination. 
> Few media of creation are so flexible, so easy to polish and rework, 
> so readily capable of realizing grand conceptual structures.

&mdash; Fred P. Brooks

---

TODO: 

- [ ] Document running Azurite for blob tests
- [ ] Docs on cache management


