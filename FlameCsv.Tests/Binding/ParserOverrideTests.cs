using System.Globalization;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers;

// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace FlameCsv.Tests.Binding;

public static class ParserOverrideTests
{
    private class TestObj
    {
        [CsvParserOverride(typeof(CurrencyParser))]
        public double Dollars { get; set; }
    }

    private class TestObj2
    {
        [CsvParserOverride<char, CurrencyParser>]
        public double Dollars { get; set; }
    }

    private sealed class CurrencyParser : ParserBase<char, double>
    {
        private readonly NumberFormatInfo _nfi = new CultureInfo("en-US").NumberFormat;

        public override bool TryParse(ReadOnlySpan<char> span, out double value)
        {
            return double.TryParse(span, NumberStyles.Currency, _nfi, out value);
        }
    }

    [Fact]
    public static void Should_Use_Custom_Parser()
    {
        const string data = "Dollars\n\"$ 8,042.15\"\n$ 123.45\n";
        var options = new CsvTextReaderOptions
        {
            HasHeader = true,
            Newline = "\n",
        };
        var objs = CsvReader.Read<TestObj>(data, options).ToList();
        Assert.Equal(2, objs.Count);
        Assert.Equal(8042.15, objs[0].Dollars);
        Assert.Equal(123.45, objs[1].Dollars);

        var objs2 = CsvReader.Read<TestObj2>(data, options).ToList();
        Assert.Equal(2, objs2.Count);
        Assert.Equal(8042.15, objs2[0].Dollars);
        Assert.Equal(123.45, objs2[1].Dollars);
    }
}
