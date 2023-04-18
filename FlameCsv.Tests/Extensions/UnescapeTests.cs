using System.Buffers;
using System.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Extensions;

public sealed class UnescapeTests : IDisposable
{
    private char[]? buffer;

    void IDisposable.Dispose() => ArrayPool<char>.Shared.EnsureReturned(ref buffer);

    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public void Should_Unescape(string input, string expected)
    {
        var delimiterCount = input.Count(c => c == '"');

        var vbo = new ValueBufferOwner<char>(ref buffer, ArrayPool<char>.Shared);
        var actualSpan = input.AsSpan().Unescape('\"', delimiterCount, vbo);
        Assert.Equal(expected, new string(actualSpan));

        char[] unescapeArray = new char[input.Length * 2];
        unescapeArray.AsSpan().Fill('\0');
        Memory<char> unescapeBuffer = unescapeArray;
        ReadOnlyMemory<char> actualMemory = input.AsMemory().Unescape('\"', delimiterCount, ref unescapeBuffer);
        Assert.Equal(expected, new string(actualMemory.Span));

        if (new Span<char>(unescapeArray).Overlaps(actualMemory.Span))
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
    public void Should_Throw_UnreachableException_On_Invalid(string input)
    {
        Assert.Throws<UnreachableException>(() =>
        {
            var vbo = new ValueBufferOwner<char>(ref buffer, ArrayPool<char>.Shared);
            input.AsSpan().Unescape('\"', 4, vbo);
        });
        Assert.Throws<UnreachableException>(() =>
        {
            Memory<char> unused = Array.Empty<char>();
            return input.AsMemory().Unescape('\"', 4, ref unused);
        });
    }
}
