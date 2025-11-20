using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public static class ScalarTests
{
    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Parse_Identically(bool isCRLF)
    {
        var options = new CsvOptions<char> { Newline = isCRLF ? CsvNewline.CRLF : CsvNewline.LF };

        var scalarTokenizer = CsvTokenizer.CreateScalar(options);
        var simdTokenizer = CsvTokenizer.Create(options);

        Assert.SkipWhen(simdTokenizer is null, "SIMD tokenizer not supported on this platform");

        using RecordBuffer rbScalar = new();
        using RecordBuffer rbSimd = new();

        FieldBuffer fbScalar = rbScalar.GetUnreadBuffer(0, out int scalarStartIndex);
        FieldBuffer fbSimd = rbSimd.GetUnreadBuffer(0, out int simdStartIndex);

        fbScalar.Fields.Clear();
        fbScalar.Quotes.Clear();
        fbSimd.Fields.Clear();
        fbSimd.Quotes.Clear();

        Assert.Equal(fbScalar.Fields, fbSimd.Fields);
        Assert.Equal(fbScalar.Quotes, fbSimd.Quotes);

        ReadOnlySpan<char> data = TestDataGenerator.GenerateText(options.Newline, true, true, Escaping.Quote).Span;

        int resultScalar = scalarTokenizer.Tokenize(fbScalar, scalarStartIndex, data, readToEnd: false);
        int resultSimd = simdTokenizer!.Tokenize(fbSimd, simdStartIndex, data);

        // scalar can read the whole data
        // simd always stops because of insufficient field buffer, as we have more data than can fit in one batch
        Assert.Equal(RecordBuffer.DefaultFieldBufferSize - 1, resultScalar);
        Assert.Equal(RecordBuffer.DefaultFieldBufferSize - simdTokenizer.MinimumFieldBufferSize, resultSimd);
        int len = resultSimd;

        Assert.Equal(fbScalar.Fields[..len], fbSimd.Fields[..len]);
        Assert.Equal(fbScalar.Quotes[..len], fbSimd.Quotes[..len]);
    }
}
