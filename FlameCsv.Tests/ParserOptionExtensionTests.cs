using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Tests;

public static class ParserOptionExtensionTests
{
    [Fact]
    public static void Should_Convert_To_Utf()
    {
        var opts = new CsvParserOptions<char>
        {
            StringDelimiter = 'x',
            Delimiter = 'y',
            NewLine = "mörkö".ToCharArray(),
        };

        var converted = opts.ToUtf8Bytes();
        Assert.Equal((byte)'x', converted.StringDelimiter);
        Assert.Equal((byte)'y', converted.Delimiter);
        Assert.Equal(
            Encoding.UTF8.GetBytes("mörkö"),
            converted.NewLine.ToArray());
    }

    [Fact]
    public static void Should_Convert_From_Utf()
    {
        var opts = new CsvParserOptions<byte>
        {
            StringDelimiter = (byte)'x',
            Delimiter = (byte)'y',
            NewLine = Encoding.UTF8.GetBytes("mörkö"),
        };

        var converted = opts.FromUtf8Bytes();
        Assert.Equal('x', converted.StringDelimiter);
        Assert.Equal('y', converted.Delimiter);
        Assert.Equal("mörkö".ToCharArray(), converted.NewLine.ToArray());
    }

    [Fact]
    public static void Should_Validate_Input()
    {
        Assert.Throws<InvalidOperationException>(
            () => new CsvParserOptions<char>
            {
                StringDelimiter = '°',
            }.ToUtf8Bytes());

        Assert.Throws<InvalidOperationException>(
            () => new CsvParserOptions<byte>
            {
                StringDelimiter = byte.MaxValue,
            }.FromUtf8Bytes());
    }
}
