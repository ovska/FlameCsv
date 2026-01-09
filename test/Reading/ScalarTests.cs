using System.Buffers;
using System.Runtime.Intrinsics;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;
using Xunit.Sdk;

namespace FlameCsv.Tests.Reading;

public static class ScalarTests
{
    public static TheoryData<Type, CsvNewline, Escaping> ParsingData() =>
        [
            .. from type in new Tokenizers.Types()
            from newline in new[] { CsvNewline.CRLF, CsvNewline.LF }
            from escaping in new[] { Escaping.None, Escaping.Quote, Escaping.QuoteNull }
            select (type, newline, escaping),
        ];

    [Theory, MemberData(nameof(ParsingData))]
    public static void Should_Parse_Identically(Type type, CsvNewline newline, Escaping escaping)
    {
        SkipIfNotSupported(type);

        const int bufferSize = RecordBuffer.DefaultFieldBufferSize;

        var options = new CsvOptions<char> { Newline = newline, Quote = escaping == Escaping.QuoteNull ? null : '"' };
        var scalarTokenizer = options.GetTokenizers().scalar;
        var simdTokenizer = Tokenizers.GetTokenizer<char>(type, options);

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

    [Theory, ClassData(typeof(Tokenizers.Types))]
    public static void Should_Parse_Long_Field(Type type)
    {
        SkipIfNotSupported(type);

        using var rb = new RecordBuffer();
        using var rb2 = new RecordBuffer();
        var (scalar, _) = CsvOptions<char>.Default.GetTokenizers();
        var tokenizer = Tokenizers.GetTokenizer<char>(type);
        int backstop = tokenizer.Overscan * 2;

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

        apbw.GetSpan(backstop).Slice(0, backstop).Fill('^');
        apbw.Advance(backstop);

        string[] values = Csv.From(apbw.WrittenMemory[..^backstop])
            .Enumerate(new CsvOptions<char> { HasHeader = false })
            .Select(
                static (r, i) =>
                {
                    // len + quotes
                    Assert.Equal(1, r.FieldCount);
                    Assert.Equal(i + 2, r.Raw.Length);
                    return StringPool.Shared.GetOrAdd(r[0]);
                }
            )
            .ToArray();
        Assert.Equal(64, values.Length);

        Span<uint> fb = rb.GetUnreadBuffer(0, out int startIndex);
        fb.Clear();
        int result = tokenizer.Tokenize(fb, startIndex, apbw.WrittenSpan);

        Assert.Equal(64, result);

        int recordsRead = rb.SetFieldsRead(result);
        Assert.Equal(64, recordsRead);

        Span<uint> fb2 = rb2.GetUnreadBuffer(0, out int startIndex2);
        fb2.Clear();
        int result2 = scalar.Tokenize(fb2, startIndex2, apbw.WrittenSpan, readToEnd: false);
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

    [Theory, ClassData(typeof(Tokenizers.Types))]
    public static void Should_Bail_On_Disjoint_CRLF(Type type)
    {
        SkipIfNotSupported(type);

        using var rb = new RecordBuffer();
        using var apbw = new ArrayPoolBufferWriter<char>();

        for (int i = 0; i < 50; i++)
        {
            apbw.Write("field1,field2");
            apbw.Write(i % 2 == 0 ? "\r\n" : "\n");
        }

        var tokenizer = Tokenizers.GetTokenizer<char>(type);

        int res = tokenizer.Tokenize(rb.GetUnreadBuffer(0, out int startIndex), startIndex, apbw.WrittenSpan);
        Assert.Equal(-1, res);
    }

    private static void SkipIfNotSupported(Type type)
    {
        bool supported = false;

        if (type == typeof(SimdTokenizer<,,>))
            supported = Vector128.IsHardwareAccelerated;
        else if (type == typeof(Avx2Tokenizer<,,>))
            supported = Avx2Tokenizer.IsSupported;
#if NET10_0_OR_GREATER
        else if (type == typeof(Avx512Tokenizer<,,>))
            supported = Avx512Tokenizer.IsSupported;
#endif
        else if (type == typeof(ArmTokenizer<,,>))
            supported = ArmTokenizer.IsSupported;

        Assert.SkipUnless(supported, $"{type.Name[..(type.Name.IndexOf('`'))]} not supported on this platform");
    }
}
