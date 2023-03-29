using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ThoughtStuff.Caching.Example.Pages;

public class CacheManagerModel : PageModel
{
    private readonly ICacheManager _cacheManager;

    public CacheManagerModel(ICacheManager cacheManager)
    {
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
    }

    public int EntryCount { get; private set; }
    public IAsyncEnumerable<string> Keys { get; private set; } = AsyncEnumerable.Empty<string>();

    public async Task OnGetAsync()
    {
        EntryCount = await _cacheManager.GetCacheEntryCount();
        Keys = _cacheManager.EnumerateKeys();
    }
}
