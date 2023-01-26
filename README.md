# ThoughtStuff.Caching

Injects look-through cache for configured services, for example:

```cs
// Cache results returned from MySlowService in Mem Cache
services.AddMethodCaching()
        .AddTransientWithCaching<IMySlowService, MySlowService, MyResult>();

var app = builder.Build();

// Configure method caching policies
var methodCachePolicies = app.Services.GetRequiredService<IMethodCacheOptionsLookup>();
methodCachePolicies.AddRelativeExpiration<ISlowExampleService>(TimeSpan.FromSeconds(30));
```

---

> The programmer, like the poet, works only slightly removed from pure **thought-stuff**. 
> He builds his castles in the air, from air, creating by exertion of the imagination. 
> Few media of creation are so flexible, so easy to polish and rework, 
> so readily capable of realizing grand conceptual structures.

&mdash; Fred P. Brooks

---

TODO: 

- [ ] Document running Azurite for blob tests
