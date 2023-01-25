// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using AutoFixture.Xunit2;
using ThoughtStuff.Caching.FileSystem;

namespace ThoughtStuff.Caching.Tests;

public class JsonCacheTest
{
    [Theory(DisplayName = "Caching JSON: Location"), AutoMoq]
    public void Locator([Frozen] ITextCache textCache, JsonCache jsonCache, string key)
    {
        var location = jsonCache.GetLocation(key);

        location.Should().Be(textCache.GetLocation(key), "it should pass through to ITextCache");
    }

    class ExampleDto
    {
        public int Count { get; set; }
    }

    [Theory(DisplayName = "Caching JSON: Wraps text cache"), CacheTest]
    public void WrapsTextCache([Frozen] ITextCache textCache, JsonCache jsonCache, string key)
    {
        var value = new ExampleDto { Count = 42 };

        jsonCache.Set(key, value);

        textCache.GetString(key).Should().Be(@"{""$id"":""1"",""Count"":42}");

        var fetched = jsonCache.Get<ExampleDto>(key);
        fetched.Should().BeEquivalentTo(value);
    }

    [Theory(DisplayName = "Caching JSON: Default values"), CacheTest]
    public void ReturnsDefault(JsonCache jsonCache, string missingKey)
    {
        var fetchedDto = jsonCache.Get<ExampleDto>(missingKey);
        fetchedDto.Should().BeNull();

        var fetchedDouble = jsonCache.Get<double>(missingKey);
        fetchedDouble.Should().Be(0.0);
    }

    [Theory(DisplayName = "Caching JSON: Throw setting default object"), AutoMoq]
    public void ThrowSettingDefault(JsonCache jsonCache, string key)
    {
        Action act = () =>
            jsonCache.Set<ExampleDto>(key, null);
        act.Should().Throw<ArgumentException>().WithMessage("*default*");
    }

    [Theory(DisplayName = "Caching JSON: Throw setting default value"), AutoMoq]
    public void ThrowSettingDefaultInt(JsonCache jsonCache, string key)
    {
        Action act = () =>
            jsonCache.Set(key, 0);
        act.Should().Throw<ArgumentException>().WithMessage("*default*");
    }
}
