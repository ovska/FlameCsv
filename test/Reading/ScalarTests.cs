using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
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
            { CsvNewline.CRLF, Escaping.QuoteNull },
            { CsvNewline.LF, Escaping.None },
            { CsvNewline.LF, Escaping.Quote },
            { CsvNewline.LF, Escaping.QuoteNull },
        };

    [Theory, MemberData(nameof(NewlineEscapingData))]
    public static void Should_Parse_Identically(CsvNewline newline, Escaping escaping)
    {
        const int bufferSize = RecordBuffer.DefaultFieldBufferSize;

        var options = new CsvOptions<char> { Newline = newline };
        var (scalarTokenizer, simdTokenizer) = options.GetTokenizers();

        Assert.SkipWhen(simdTokenizer is null, "SIMD tokenizer not supported on this platform");

        using RecordBuffer rbScalar = new(bufferSize);
        using RecordBuffer rbSimd = new(bufferSize);

        Span<uint> fbScalar = rbScalar.GetUnreadBuffer(0, out int scalarStartIndex);
        Span<uint> fbSimd = rbSimd.GetUnreadBuffer(0, out int simdStartIndex);

        fbScalar.Clear();
        fbSimd.Clear();

        ReadOnlySpan<char> data = TestDataGenerator
            .GenerateText(options.Newline, true, escaping != Escaping.None)
            .AsSpan();

        int resultScalar = scalarTokenizer.Tokenize(fbScalar, scalarStartIndex, data, readToEnd: false);
        int resultSimd = simdTokenizer.Tokenize(fbSimd, simdStartIndex, data);

        // scalar can read the whole data
        // simd always stops because of insufficient field buffer, as we have more data than can fit in one batch
        Assert.Equal(bufferSize - 1, resultScalar);
        Assert.Equal(bufferSize - simdTokenizer.MaxFieldsPerIteration, resultSimd);

        int len = resultSimd;
        Assert.Equal(fbScalar[..len], fbSimd[..len]);
    }

    [Fact]
    public static void Should_Parse_Long_Field()
    {
        using var rb = new RecordBuffer();
        using var rb2 = new RecordBuffer();
        var (scalar, tokenizer) = CsvOptions<char>.Default.GetTokenizers();

        Assert.SkipWhen(tokenizer is null, "SIMD tokenizer not supported on this platform");

        char[] data;

        using (var apbw = new ArrayPoolBufferWriter<char>())
        {
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

            apbw.GetSpan(128).Slice(0, 128).Fill('^');
            apbw.Advance(128);
            data = apbw.WrittenSpan.ToArray();
        }

        string[] values = Csv.From(data.AsMemory(..^128))
            .Enumerate(new CsvOptions<char> { HasHeader = false })
            .Select(
                static (r, i) =>
                {
                    // len + quotes
                    Assert.Equal(i + 2, r.Raw.Length);
                    return r[0].ToString();
                }
            )
            .ToArray();
        Assert.Equal(64, values.Length);

        Span<uint> fb = rb.GetUnreadBuffer(0, out int startIndex);
        fb.Clear();
        int result = tokenizer.Tokenize(fb, startIndex, data);
        Assert.Equal(64, result);

        int recordsRead = rb.SetFieldsRead(result);
        Assert.Equal(64, recordsRead);

        Span<uint> fb2 = rb2.GetUnreadBuffer(0, out int startIndex2);
        fb2.Clear();
        int result2 = scalar.Tokenize(fb2, startIndex2, data, readToEnd: false);
        Assert.Equal(64, result2);
        int recordsRead2 = rb2.SetFieldsRead(result2);
        Assert.Equal(64, recordsRead2);

        Assert.Equal(rb.BufferedRecordLength, rb2.BufferedRecordLength);
        Assert.Equal(rb._fields.AsSpan(0, rb._fieldCount + 1), rb2._fields.AsSpan(0, rb2._fieldCount + 1));

        for (int i = 0; i < 64; i++)
        {
            Assert.True(rb.TryPop(out var view));
            Assert.True(1 == view.Length, $"Field {i} length mismatch: {view.Length}");
            Assert.Equal(i + 3, rb.GetLengthWithNewline(view)); // quotes + newline
        }

        using var apbw2 = new ArrayPoolBufferWriter<char>();

        for (int i = 0; i < 64; i++)
        {
            Assert.True(
                apbw2.WrittenSpan.Equals(values[i], StringComparison.Ordinal),
                $"Field {i} content mismatch (len {values[i].Length}): {values[i]}"
            );

            apbw2.Write('a');
        }
    }
}
