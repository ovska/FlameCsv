using System.Globalization;
using System.Net;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public class FieldWritingExtensionsTests
{
    [Fact]
    public void Normal()
    {
        Assert.Equal("123", Chars(w => w.FormatValue(123)));
        Assert.Equal("123", Bytes(w => w.FormatValue(123)));
    }

    [Fact]
    public void NullableStructWithValue()
    {
        Assert.Equal("123", Chars(w => w.FormatValue(new int?(123))));
        Assert.Equal("123", Bytes(w => w.FormatValue(new int?(123))));
    }

    [Fact]
    public void NullableStructWithoutValue()
    {
        Assert.Equal("", Chars(w => w.FormatValue(new int?())));
        Assert.Equal("", Bytes(w => w.FormatValue(new int?())));
        Assert.Equal("<null>", Bytes(w => w.FormatValue(new int?()), new CsvOptions<byte> { Null = "<null>" }));
        Assert.Equal(
            "<null>",
            Bytes(w => w.FormatValue(new int?()), new CsvOptions<byte> { NullTokens = { [typeof(int)] = "<null>" } })
        );
    }

    [Fact]
    public void ReferenceTypeNull()
    {
        Assert.Equal("", Chars(w => w.FormatValue<IPAddress>(null!)));
        Assert.Equal("", Bytes(w => w.FormatValue<IPAddress>(null!)));
        Assert.Equal("<null>", Bytes(w => w.FormatValue<IPAddress>(null!), new CsvOptions<byte> { Null = "<null>" }));
        Assert.Equal(
            "<null>",
            Bytes(
                w => w.FormatValue<IPAddress>(null!),
                new CsvOptions<byte> { NullTokens = { [typeof(IPAddress)] = "<null>" } }
            )
        );
    }

    [Fact]
    public void ReferenceTypeValue()
    {
        var ip = IPAddress.Parse("127.0.0.1");
        Assert.Equal("127.0.0.1", Chars(w => w.FormatValue(ip)));
        Assert.Equal("127.0.0.1", Bytes(w => w.FormatValue(ip)));
    }

    [Fact]
    public void CustomFormat()
    {
        var dateTime = new DateTime(2023, 10, 1, 12, 0, 0);
        Assert.Equal("2023-10-01T12:00:00.0000000", Chars(w => w.FormatValue(dateTime, format: "O")));
        Assert.Equal("2023-10-01T12:00:00.0000000", Bytes(w => w.FormatValue(dateTime, format: "O")));
    }

    [Fact]
    public void CustomFormatProvider()
    {
        var doubleValue = 123.456;
        var formatProvider = new NumberFormatInfo { NumberDecimalSeparator = "_" };
        Assert.Equal("123_456", Chars(w => w.FormatValue(doubleValue, format: "F3", formatProvider: formatProvider)));
        Assert.Equal("123_456", Bytes(w => w.FormatValue(doubleValue, format: "F3", formatProvider: formatProvider)));
    }

    [Fact]
    public void ShouldEscape()
    {
        var nfi = new NumberFormatInfo { NumberDecimalSeparator = "," };
        Assert.Equal("\"1,23\"", Chars(w => w.FormatValue(1.23, format: "F2", formatProvider: nfi)));
        Assert.Equal("\"1,23\"", Bytes(w => w.FormatValue(1.23, format: "F2", formatProvider: nfi)));
    }

    private static string Chars(Action<CsvFieldWriter<char>> func, CsvOptions<char>? options = null)
    {
        var sb = new StringBuilder();
        var bufferWriter = new StringBuilderBufferWriter(sb, null);
        using var writer = new CsvFieldWriter<char>(bufferWriter, options ?? CsvOptions<char>.Default);
        func(writer);
        bufferWriter.Complete(null);
        return sb.ToString();
    }

    private static string Bytes(Action<CsvFieldWriter<byte>> func, CsvOptions<byte>? options = null)
    {
        using var apbw = new ArrayPoolBufferWriter<byte>();
        var bufferWriter = new StreamBufferWriter(apbw.AsStream(), default);
        using var writer = new CsvFieldWriter<byte>(bufferWriter, options ?? CsvOptions<byte>.Default);
        func(writer);
        bufferWriter.Complete(null);
        return Encoding.UTF8.GetString(apbw.WrittenSpan);
    }
}
