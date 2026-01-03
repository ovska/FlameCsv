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

        var options = new CsvOptions<char> { Newline = newline, Quote = escaping == Escaping.QuoteNull ? null : '"' };
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

        // data doesn't fully fit in the buffer
        Assert.Equal(bufferSize - 1, resultScalar);
        Assert.InRange(resultSimd, bufferSize - simdTokenizer.MaxFieldsPerIteration, bufferSize - 1);

        int len = resultSimd;
        Assert.Equal(fbScalar[..len], fbSimd[..len]);
    }

    public static TheoryData<bool, PoisonPagePlacement> DenseFieldData() =>
        [.. from scalar in GlobalData.Booleans from placement in GlobalData.PoisonPlacement select (scalar, placement)];

    [Theory, MemberData(nameof(DenseFieldData))]
    public static void Should_Parse_Dense_Fields(bool useScalar, PoisonPagePlacement placement)
    {
        using var owner = BoundedMemory.AllocateLoose<byte>(512 * 5 + 128, placement);
        Span<byte> data;

        using (var apbw = new ArrayPoolBufferWriter<byte>())
        {
            for (int i = 0; i < 512; i++)
            {
                Span<byte> span = apbw.GetSpan(5);
                span[0] = (byte)',';
                span[1] = (byte)',';
                span[2] = (byte)',';
                span[3] = (byte)',';
                span[4] = (byte)'\n';
                apbw.Advance(5);
            }

            // fill with sentinel bytes
            apbw.GetSpan(128).Slice(0, 128).Fill((byte)'^');
            apbw.Advance(128);

            if (placement is PoisonPagePlacement.After)
            {
                // copy to end
                data = owner.Memory.Span.Slice(owner.Memory.Length - apbw.WrittenSpan.Length, apbw.WrittenSpan.Length);
                apbw.WrittenSpan.CopyTo(data);
            }
            else
            {
                data = owner.Memory.Span.Slice(0, apbw.WrittenSpan.Length);
                apbw.WrittenSpan.CopyTo(data);
            }
        }

        using var rb = new RecordBuffer();

        var (scalar, tokenizer) = CsvOptions<byte>.Default.GetTokenizers();
        var dst = rb.GetUnreadBuffer(0, out int startIndex);
        int count;

        if (useScalar)
        {
            count = scalar.Tokenize(dst, startIndex, data, false);
        }
        else
        {
            Assert.SkipWhen(tokenizer is null, "SIMD tokenizer not supported on this platform");
            count = tokenizer.Tokenize(dst, startIndex, data);
        }

        Assert.Equal(512 * 5, count);
        int records = rb.SetFieldsRead(count);
        Assert.Equal(512, records);

        IEnumerable<uint> expected = Enumerable
            .Range(0, 512 * 5)
            .Select(i => (uint)i | ((i % 5) == 4 ? Field.IsEOL : 0u));

        Assert.Equal(expected, rb._fields.Skip(1).Take(count));
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
