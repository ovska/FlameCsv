using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public static class ScalarTests
{
    public static TheoryData<CsvNewline, Escaping> NewlineEscapingData =>
        new()
        {
            { CsvNewline.CRLF, Escaping.None },
            { CsvNewline.CRLF, Escaping.Quote },
            { CsvNewline.LF, Escaping.None },
            { CsvNewline.LF, Escaping.Quote },
        };

    [Theory, MemberData(nameof(NewlineEscapingData))]
    public static void Should_Parse_Identically(CsvNewline newline, Escaping escaping)
    {
        var options = new CsvOptions<char> { Newline = newline };
        var (scalarTokenizer, simdTokenizer) = options.GetTokenizers();

        Assert.SkipWhen(simdTokenizer is null, "SIMD tokenizer not supported on this platform");

        using RecordBuffer rbScalar = new();
        using RecordBuffer rbSimd = new();

        FieldBuffer fbScalar = rbScalar.GetUnreadBuffer(0, out int scalarStartIndex);
        FieldBuffer fbSimd = rbSimd.GetUnreadBuffer(0, out int simdStartIndex);

        fbScalar.Fields.Clear();
        fbScalar.Quotes.Clear();
        fbSimd.Fields.Clear();
        fbSimd.Quotes.Clear();

        ReadOnlySpan<char> data = TestDataGenerator.GenerateText(options.Newline, true, escaping);

        int resultScalar = scalarTokenizer.Tokenize(fbScalar, scalarStartIndex, data, readToEnd: false);
        int resultSimd = simdTokenizer.Tokenize(fbSimd, simdStartIndex, data);

        // scalar can read the whole data
        // simd always stops because of insufficient field buffer, as we have more data than can fit in one batch
        Assert.Equal(RecordBuffer.DefaultFieldBufferSize - 1, resultScalar);
        Assert.Equal(RecordBuffer.DefaultFieldBufferSize - simdTokenizer.MaxFieldsPerIteration, resultSimd);
        int len = resultSimd;

        Assert.Equal(fbScalar.Fields[..len], fbSimd.Fields[..len]);
        Assert.Equal(fbScalar.Quotes[..len], fbSimd.Quotes[..len]);
    }

    [Fact]
    public static void Should_Parse_Long_Field()
    {
        using var rb = new RecordBuffer();
        var tokenizer = CsvOptions<char>.Default.GetTokenizers().simd!;

        Assert.SkipWhen(tokenizer is null, "SIMD tokenizer not supported on this platform");

        FieldBuffer fb = rb.GetUnreadBuffer(0, out int startIndex);
        fb.Fields.Clear();
        fb.Quotes.Clear();

        using var apbw = new ArrayPoolBufferWriter<char>();

        for (int i = 0; i < 64; i++)
        {
            int fieldLength = i + 3;
            var span = apbw.GetSpan(fieldLength);
            span = span[..fieldLength];
            span.Fill('a');
            span[0] = '"';
            span[fieldLength - 2] = '"';
            span[fieldLength - 1] = '\n';
            apbw.Advance(fieldLength);
        }

        apbw.GetSpan(64).Slice(0, 64).Clear();
        apbw.Advance(64);

        int result = tokenizer.Tokenize(fb, startIndex, apbw.WrittenSpan);
        Assert.Equal(64, result);

        int recordsRead = rb.SetFieldsRead(result);

        Assert.Equal(64, recordsRead);

        for (int i = 0; i < 64; i++)
        {
            Assert.True(rb.TryPop(out var view));
            Assert.Equal(1, view.Length);
            Assert.Equal(i + 3, rb.GetLengthWithNewline(view)); // quotes + newline
        }
    }
}
