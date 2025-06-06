using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Extensions;

public static class ReadExtensionTests
{
    [Fact]
    public static void Should_Format_Utf8()
    {
        byte[] buffer = new byte[1024];
        int value = 123456789;

        Assert.True(ReadExtensions.TryFormatToUtf8(buffer, value, null, null, out int written));
        Assert.Equal("123456789", Encoding.UTF8.GetString(buffer.AsSpan(0, written)));

        Assert.True(ReadExtensions.TryFormatToUtf8(buffer, 0.77777, "F3", null, out written));
        Assert.Equal("0.778", Encoding.UTF8.GetString(buffer.AsSpan(0, written)));

        Assert.True(
            ReadExtensions.TryFormatToUtf8(
                buffer,
                0.5,
                null,
                new NumberFormatInfo { NumberDecimalSeparator = "," },
                out written
            )
        );
        Assert.Equal("0,5", Encoding.UTF8.GetString(buffer.AsSpan(0, written)));
    }

    [Fact]
    public static void Should_Parse_Utf8_Long()
    {
        Assert.False(ReadExtensions.TryParseFromUtf8([], null, out int _));

        ReadOnlySpan<byte> data = "0.99999999999999999999999999999999999999999999999999999999999999999999999"u8;
        Assert.True(ReadExtensions.TryParseFromUtf8(data, null, out double value));
        Assert.Equal(1, value);

        using var apbw = new ArrayPoolBufferWriter<byte>(1024);
        var span = apbw.GetSpan(1024);
        span.Fill((byte)' ');
        span[^1] = (byte)'5';
        apbw.Advance(1024);

        Assert.True(ReadExtensions.TryParseFromUtf8(apbw.WrittenSpan, null, out int intValue));
        Assert.Equal(5, intValue);
    }

    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Trim(CsvFieldTrimming value, string input, string expected)
    {
        Assert.Equal(expected, input.AsSpan().Trim(value).ToString());
        Assert.Equal(expected, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input).AsSpan().Trim(value)));
    }

    [Theory, MemberData(nameof(TrimmingData))]
    public static void Should_Check_If_Needs_Trimming(CsvFieldTrimming value, string input, string expected)
    {
        if (value == CsvFieldTrimming.None)
        {
            Assert.False(input.AsSpan().NeedsTrimming(value));
            return;
        }

        Assert.Equal(input.Length != expected.Length, input.AsSpan().NeedsTrimming(value));
    }

    public static TheoryData<CsvFieldTrimming, string, string> TrimmingData =>
        new()
        {
            { CsvFieldTrimming.None, "  abc  ", "  abc  " },
            { CsvFieldTrimming.Leading, "  abc  ", "abc  " },
            { CsvFieldTrimming.Trailing, "  abc  ", "  abc" },
            { CsvFieldTrimming.Both, "  abc  ", "abc" },
            { CsvFieldTrimming.None, "", "" },
            { CsvFieldTrimming.Leading, "", "" },
            { CsvFieldTrimming.Trailing, "", "" },
            { CsvFieldTrimming.Both, "", "" },
            { CsvFieldTrimming.None, " ", " " },
            { CsvFieldTrimming.Leading, " ", "" },
            { CsvFieldTrimming.Trailing, " ", "" },
            { CsvFieldTrimming.Both, " ", "" },
        };
}
