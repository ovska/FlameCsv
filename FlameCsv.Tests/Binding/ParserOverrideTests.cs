using System.Globalization;
using FlameCsv.Binding.Attributes;
using FlameCsv.Extensions;
using FlameCsv.Parsers;
using FlameCsv.Readers;

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

    private sealed class CurrencyParser : ParserBase<char, double>
    {
        private readonly NumberFormatInfo _nfi = new CultureInfo("en-US").NumberFormat;

        public override bool TryParse(ReadOnlySpan<char> span, out double value)
        {
            return double.TryParse(span, NumberStyles.Currency, _nfi, out value);
        }
    }

    [Fact(Skip = "TODO FIXME: last column quoted breaks the enumerator")]
    public static void Should_Use_Custom_Parser()
    {
        const string data = "Dollars\n\"$ 8,042.15\"\n$ 123.45\n";
        var options = CsvReaderOptions<char>.Default;
        options.Tokens = options.Tokens.WithNewLine("\n");
        options.HasHeader = true;
        var objs = CsvReader.Read<TestObj>(data, options).ToList();
        Assert.Equal(2, objs.Count);
        Assert.Equal(8042.15, objs[0].Dollars);
        Assert.Equal(123.45, objs[1].Dollars);
    }
}
