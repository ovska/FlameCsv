using System.Globalization;
using System.Text;
using FlameCsv.Attributes;

namespace FlameCsv.Tests.Binding;

public static partial class ConverterOverrideTests
{
    [CsvTypeMap<char, TestObj>]
    private partial class CharTypeMap;

    [CsvTypeMap<byte, TestObj>]
    private partial class ByteTypeMap;

    private class TestObj
    {
        [CsvConverter<CurrencyConverter>]
        public double Dollars { get; set; }

        [CsvStringPooling]
        public string? Pooled { get; set; }
    }

    private sealed class CurrencyConverter : CsvConverter<char, double>
    {
        public override bool TryFormat(Span<char> destination, double value, out int charsWritten)
        {
            throw new NotSupportedException();
        }

        public override bool TryParse(ReadOnlySpan<char> source, out double value)
        {
            return double.TryParse(source, NumberStyles.Currency, new CultureInfo("en-US").NumberFormat, out value);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Use_Custom_Converter(bool sourceGen)
    {
        const string data = "Dollars\n\"$ 8,042.15\"\n$ 123.45\n";
        var options = new CsvOptions<char> { HasHeader = true, Newline = CsvNewline.LF };
        var objs = sourceGen
            ? Csv.From(data).Read(CharTypeMap.Default, options).ToList()
            : Csv.From(data).Read<TestObj>(options).ToList();
        Assert.Equal(2, objs.Count);
        Assert.Equal(8042.15, objs[0].Dollars);
        Assert.Equal(123.45, objs[1].Dollars);
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Use_String_Pooling(bool sourceGen)
    {
        byte[] data = "Pooled\nabc\n\nabc\n"u8.ToArray();
        var objs = sourceGen
            ? Csv.From(data).Read(ByteTypeMap.Default).ToList()
            : Csv.From(data).Read<TestObj>().ToList();
        Assert.Equal(3, objs.Count);
        Assert.Same(objs[0].Pooled, objs[2].Pooled);

        using MemoryStream ms = new();

        if (sourceGen)
        {
            Csv.To(ms).Write(ByteTypeMap.Default, objs);
        }
        else
        {
            Csv.To(ms).Write(objs);
        }

        Assert.Equal("Dollars,Pooled\r\n0,abc\r\n0,\r\n0,abc\r\n", Encoding.UTF8.GetString(ms.ToArray()));
    }
}
