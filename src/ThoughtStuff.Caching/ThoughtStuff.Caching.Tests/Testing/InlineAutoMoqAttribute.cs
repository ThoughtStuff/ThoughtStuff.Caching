namespace ThoughtStuff.Caching.Tests.Testing;

/// <summary>
/// Allows use of Inline data for some parameters with Moq generation of remaining parameters.
/// Declare inline data parameters first.
/// </summary>
public class InlineAutoMoqAttribute : InlineAutoDataAttribute
{
    public InlineAutoMoqAttribute(params object?[] values)
        : base(AutoMoqAttribute.BuildFixture, values)
    {
    }
}
