// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Caching.Azure;

namespace ThoughtStuff.Caching.Tests.Azure;

public class ObjectDictionaryConverterTest
{
    public class ExampleItem
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public DateTimeOffset? Date { get; set; }
        public TimeSpan? TimeSpan { get; set; }
    }

    [Theory(DisplayName = "ObjectDictionary: To Dictionary"), AutoMoq]
    public void ConvertingToDictionary(ObjectDictionaryConverter subject, string name, int age)
    {
        var item = new ExampleItem
        {
            Name = name,
            Age = age
        };
        var dictionary = subject.ConvertToDictionary(item);
        dictionary.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                ["Name"] = $"\"{item.Name}\"",
                ["Age"] = $"{item.Age}",
                ["Date"] = "null",
                ["TimeSpan"] = "null",
            });
    }

    [Theory(DisplayName = "ObjectDictionary: To Object"), AutoMoq]
    public void ConvertingToObject(ObjectDictionaryConverter subject,
                                   string name,
                                   int age,
                                   DateTimeOffset date,
                                   TimeSpan timeSpan)
    {
        var dictionary = new Dictionary<string, string>
        {
            ["Name"] = $"\"{name}\"",
            ["Age"] = $"\"{age}\"",
            ["Date"] = $"\"{date:o}\"",    // Round-trip ISO 8601 compatible
            ["TimeSpan"] = $"\"{timeSpan}\""
        };

        var item = subject.ConvertToObject<ExampleItem>(dictionary);

        item.Should().BeEquivalentTo(new ExampleItem
        {
            Name = name,
            Age = age,
            Date = date,
            TimeSpan = timeSpan
        });
    }

    [Theory(DisplayName = "ObjectDictionary: Round Trip"), AutoMoq]
    public void RoundTrip(ObjectDictionaryConverter subject, ExampleItem item)
    {
        var dictionary = subject.ConvertToDictionary(item);
        var returned = subject.ConvertToObject<ExampleItem>(dictionary);
        returned.Should().BeEquivalentTo(item);
    }

    [Theory(DisplayName = "ObjectDictionary: Round Trip Empty"), AutoMoq]
    public void RoundTripEmpty(ObjectDictionaryConverter subject)
    {
        var item = new ExampleItem();
        var dictionary = subject.ConvertToDictionary(item);
        var returned = subject.ConvertToObject<ExampleItem>(dictionary);
        returned.Should().BeEquivalentTo(item);
    }
}
