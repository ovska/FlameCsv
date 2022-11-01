using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Tests.Extensions;

public class StringDelimiterExtensionTests
{
    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public static void Should_Unescape(string input, string expected)
    {
        using var buffer = new BufferOwner<char>();
        var delimiterCount = input.Count(c => c == '"');
        var actual = input.AsSpan().Unescape('\"', delimiterCount, buffer);
        Assert.Equal(expected, new string(actual));
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("\"")]
    [InlineData("\"test")]
    [InlineData("test\"")]
    [InlineData("\"te\"st\"")]
    [InlineData("\"\"test\"")]
    [InlineData("\"test\"\"")]
    public static void Should_Throw_On_Invalid(string input)
    {
        using var buffer = new BufferOwner<char>();
        Assert.Throws<InvalidOperationException>(() => input.AsSpan().Unescape('\"', 4, buffer));
    }
}
