// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class JsonTypedCacheTest
{
    [Theory(DisplayName = "Caching JSON: Location"), AutoMoq]
    public void Locator([Frozen] ITextCache textCache, JsonTypedCache subject, string key)
    {
        var location = subject.GetLocation(key);

        location.Should().Be(textCache.GetLocation(key), "it should pass through to ITextCache");
    }

    class ExampleDto
    {
        public int Count { get; set; }
    }

    [Theory(DisplayName = "Caching JSON: Wraps text cache"), CacheTest]
    public void WrapsTextCache([Frozen] ITextCache textCache, JsonTypedCache subject, string key)
    {
        var value = new ExampleDto { Count = 42 };

        subject.Set(key, value);

        textCache.GetString(key).Should().Be(@"{""$id"":""1"",""Count"":42}");

        var fetched = subject.Get<ExampleDto>(key);
        fetched.Should().BeEquivalentTo(value);
    }

    [Theory(DisplayName = "Caching JSON: Remove"), CacheTest]
    public void Removal([Frozen] ITextCache textCache, JsonTypedCache subject, string key)
    {
        subject.Set(key, 42);

        subject.Remove(key);

        textCache.GetString(key).Should().BeNull();
        subject.Get<int?>(key).Should().BeNull();
    }

    [Theory(DisplayName = "Caching JSON: Remove Absent"), CacheTest]
    public void RemoveAbsent(JsonTypedCache subject, string key)
    {
        // Does not throw
        subject.Remove(key);
    }

    [Theory(DisplayName = "Caching JSON: Default values"), CacheTest]
    public void ReturnsDefault(JsonTypedCache subject, string missingKey)
    {
        var fetchedDto = subject.Get<ExampleDto>(missingKey);
        fetchedDto.Should().BeNull();

        var fetchedDouble = subject.Get<double>(missingKey);
        fetchedDouble.Should().Be(0.0);
    }

    [Theory(DisplayName = "Caching JSON: Throw setting default object"), AutoMoq]
    public void ThrowSettingDefault(JsonTypedCache subject, string key)
    {
        Action act = () =>
            subject.Set<ExampleDto>(key, null!);
        act.Should().Throw<ArgumentException>().WithMessage("*default*");
    }

    [Theory(DisplayName = "Caching JSON: Throw setting default value"), AutoMoq]
    public void ThrowSettingDefaultInt(JsonTypedCache subject, string key)
    {
        Action act = () =>
            subject.Set(key, 0);
        act.Should().Throw<ArgumentException>().WithMessage("*default*");
    }
}
