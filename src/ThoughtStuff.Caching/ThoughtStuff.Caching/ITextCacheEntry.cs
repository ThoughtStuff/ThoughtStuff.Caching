// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching;

public interface ITextCacheEntry : ICacheEntry
{
    string Value { get; }
}
