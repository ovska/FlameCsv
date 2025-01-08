using System.Buffers;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Readers;

public static class NewlineDetectTests
{
    private static ReadOnlyMemory<byte> U8(string input) => Encoding.UTF8.GetBytes(input);

    [Theory]
    [InlineData("A,B,C\r\nX,Y,Z\r\n", false)]
    [InlineData("A,B,C\nX,Y,Z\n", false)]
    [InlineData("A,B,C\r\n\"X\",Y,Z\r\n", false)]
    [InlineData("A,B,C\n\"X\",Y,Z\n", false)]
    [InlineData("A,B,C\r\n\"\"\"X\",Y,Z\r\n", false)]
    [InlineData("A,B,C\n\"\"\"X\",Y,Z\n", false)]
    [InlineData("A,B,C\r\nX,Y,Z\r\n", true)]
    [InlineData("A,B,C\nX,Y,Z\n", true)]
    [InlineData("A,B,C\r\n\"X\",Y,Z\r\n", true)]
    [InlineData("A,B,C\n\"X\",Y,Z\n", true)]
    [InlineData("A,B,C\r\n\"^\"X\",Y,Z\r\n", true)]
    [InlineData("A,B,C\n\"^\"X\",Y,Z\n", true)]
    public static void RFC4180(string input, bool escape)
    {
        // repeat to ensure the newline detection is valid for the parser instance, not whole Options
        var textOpts = new CsvOptions<char> { Newline = null, Escape = escape ? '^' : null };
        var utf8Opts = new CsvOptions<byte> { Newline = default, Escape = escape ? '^' : null };

        for (int i = 0; i < 2; i++)
        {
            Impl(textOpts, input.AsMemory(), "A,B,C");
            Impl(utf8Opts, U8(input), "A,B,C"u8);
        }

        Assert.Empty(textOpts.Newline ?? "");
        Assert.Empty(utf8Opts.Newline ?? "");
    }

    private static void Impl<T>(
        CsvOptions<T> options,
        ReadOnlyMemory<T> input,
        ReadOnlySpan<T> expected) where T : unmanaged, IBinaryInteger<T>
    {
        using var parser = CsvParser<T>.Create(options);
        parser.Reset(new ReadOnlySequence<T>(input));
        Assert.True(parser.TryReadLine(out var line, out _, isFinalBlock: false));
        Assert.Equal(expected, line.Span);
        Assert.True(parser.TryReadLine(out _, out _, isFinalBlock: false));
    }

    [Fact]
    public static void Should_Throw_On_Very_Long_Inputs()
    {
        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Newline = null });
        parser.Reset(new ReadOnlySequence<char>(new string('x', 4096).AsMemory()));

        Assert.Throws<CsvConfigurationException>(() =>
        {
            _ = parser.TryReadLine(out _, out _, false);
        });
    }
}
