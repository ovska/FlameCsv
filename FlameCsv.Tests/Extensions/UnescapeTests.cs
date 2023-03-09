using System.Buffers;
using System.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Tests.Extensions;

public sealed class UnescapeTests : IDisposable
{
    private ValueBufferOwner<char> RB => new(ref buffer, ArrayPool<char>.Shared);

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
        var actualSpan = input.AsSpan().Unescape('\"', delimiterCount, RB);
        Assert.Equal(expected, new string(actualSpan));

        using var bo = new BufferOwner<char>(ArrayPool<char>.Shared);
        var actualMemory = input.AsMemory().Unescape('\"', delimiterCount, bo);
        Assert.Equal(expected, new string(actualMemory.Span));
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
        using var bo = new BufferOwner<char>(ArrayPool<char>.Shared);
        Assert.Throws<UnreachableException>(() => input.AsSpan().Unescape('\"', 4, RB));
        Assert.Throws<UnreachableException>(() => input.AsMemory().Unescape('\"', 4, bo));
    }
}
