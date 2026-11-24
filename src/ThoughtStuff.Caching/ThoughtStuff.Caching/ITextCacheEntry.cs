namespace ThoughtStuff.Caching;

public interface ITextCacheEntry : ICacheEntry
{
    string Value { get; }
}
