using System.Globalization;
using FlameCsv.Attributes;

namespace FlameCsv.Tests.Binding;

public static class ConverterOverrideTests
{
    private class TestObj
    {
        [CsvConverter<CurrencyConverter>]
        public double Dollars { get; set; }
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

    [Fact]
    public static void Should_Use_Custom_Converter()
    {
        const string data = "Dollars\n\"$ 8,042.15\"\n$ 123.45\n";
        var options = new CsvOptions<char>
        {
            HasHeader = true,
            Newline = "\n",
        };
        var objs = CsvReader.Read<TestObj>(data, options).ToList();
        Assert.Equal(2, objs.Count);
        Assert.Equal(8042.15, objs[0].Dollars);
        Assert.Equal(123.45, objs[1].Dollars);
    }
}
