using System.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Extensions;

public static class UnescapeTests
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
        var delimiterCount = input.Count(c => c == '"');

        char[] unescapeArray = new char[input.Length * 2];
        unescapeArray.AsSpan().Fill('\0');
        Memory<char> unescapeBuffer = unescapeArray;
        ReadOnlyMemory<char> actualMemory = input.AsMemory().Unescape('\"', delimiterCount, ref unescapeBuffer);
        Assert.Equal(expected, new string(actualMemory.Span));

        if (actualMemory.Span.Overlaps(unescapeArray))
        {
            Assert.Equal(unescapeArray.Length - actualMemory.Length, unescapeBuffer.Length);
            Assert.Equal(actualMemory.ToArray(), unescapeArray.AsMemory(0, actualMemory.Length).ToArray());
            Assert.All(unescapeArray.AsMemory(actualMemory.Length).ToArray(), c => Assert.Equal('\0', c));
        }
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
    public static void Should_Throw_UnreachableException_On_Invalid(string input)
    {
        Assert.Throws<UnreachableException>(() =>
        {
            Memory<char> unused = Array.Empty<char>();
            return input.AsMemory().Unescape('\"', 4, ref unused);
        });
    }
}
