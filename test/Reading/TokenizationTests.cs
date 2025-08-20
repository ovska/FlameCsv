using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Intrinsics;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Reading;

public class TokenizationTests
{
    public enum RecSep
    {
        LF,
        CRLF,
        Alternating,
        CR,
    }

    public const int RecordCount = 1_000;

    // platform uses CRLF parser, but alternates between CRLF and LF, and inserts a lone CR every now and then
    public static TheoryData<RecSep> NewlineData => new() { RecSep.LF, RecSep.CRLF, RecSep.Alternating, RecSep.CR };

    [Theory, MemberData(nameof(NewlineData))]
    public void Avx2_Char(RecSep newline)
    {
        Assert.SkipUnless(Avx2Tokenizer.IsSupported, "AVX2 is not supported on this platform.");

        TokenizeCore<char>(
            newline,
            newline == RecSep.LF
                ? new Avx2Tokenizer<char, NewlineLF>(CsvOptions<char>.Default)
                : new Avx2Tokenizer<char, NewlineCRLF>(CsvOptions<char>.Default)
        );
    }

    [Theory, MemberData(nameof(NewlineData))]
    public void Avx2_Byte(RecSep newline)
    {
        Assert.SkipUnless(Avx2Tokenizer.IsSupported, "AVX2 is not supported on this platform.");

        TokenizeCore<byte>(
            newline,
            newline == RecSep.LF
                ? new Avx2Tokenizer<byte, NewlineLF>(CsvOptions<byte>.Default)
                : new Avx2Tokenizer<byte, NewlineCRLF>(CsvOptions<byte>.Default)
        );
    }

    [Theory, MemberData(nameof(NewlineData))]
    public void Generic_Char(RecSep newline)
    {
        TokenizeCore<char>(
            newline,
            newline == RecSep.LF
                ? new SimdTokenizer<char, NewlineLF>(CsvOptions<char>.Default)
                : new SimdTokenizer<char, NewlineCRLF>(CsvOptions<char>.Default)
        );
    }

    [Theory, MemberData(nameof(NewlineData))]
    public void Generic_Byte(RecSep newline)
    {
        TokenizeCore<byte>(
            newline,
            newline == RecSep.LF
                ? new SimdTokenizer<byte, NewlineLF>(CsvOptions<byte>.Default)
                : new SimdTokenizer<byte, NewlineCRLF>(CsvOptions<byte>.Default)
        );
    }

    private static void TokenizeCore<T>(RecSep newline, CsvPartialTokenizer<T> tokenizer)
        where T : unmanaged, IBinaryInteger<T>
    {
        using var rb = new RecordBuffer();
        var dst = rb.GetUnreadBuffer(tokenizer.MinimumFieldBufferSize, out int startIndex);

        ReadOnlySpan<T> dataset = GetDataset<T>(newline);
        Assert.NotEqual(0, tokenizer.Tokenize(dst, startIndex, dataset));

        RecordView expected = GetExpected(newline);
        var a = expected._fields[10..13];
        var b = rb.GetFieldArrayRef().AsSpan(1, 600)[10..13];
        Assert.Equal(expected._fields.AsSpan(0, 600), rb.GetFieldArrayRef().AsSpan(1, 600));
        Assert.Equal(expected._quotes.AsSpan(0, 600), rb.GetQuoteArrayRef().AsSpan(1, 600));
    }

    private static RecordView GetExpected(RecSep newline)
    {
        ArrayBufferWriter<uint> fields = new();
        ArrayBufferWriter<byte> quotes = new();

        int i;
        uint idx = 0;

        for (i = 0; i < RecordCount; i++)
        {
            Span<uint> f = fields.GetSpan(6);

            f[0] = (idx += 2) - 1;
            f[1] = (idx += 6);
            f[2] = (idx += 20);
            f[3] = (idx += 15);
            f[4] = (idx += 20);

            uint flag = GetEOLFlag();
            f[5] = (idx += 15) | flag;
            idx += flag == Field.IsCRLF ? 2u : 1u;

            fields.Advance(6);

            Span<byte> q = quotes.GetSpan(6);
            q[0] = 0;
            q[1] = 0;
            q[2] = 0;
            q[3] = 2; // quoted
            q[4] = 0;
            q[5] = 2; // quoted
            quotes.Advance(6);
        }

        Assert.True(MemoryMarshal.TryGetArray(fields.WrittenMemory, out ArraySegment<uint> fieldSegment));
        Assert.True(MemoryMarshal.TryGetArray(quotes.WrittenMemory, out ArraySegment<byte> quoteSegment));

        return new RecordView(fieldSegment.Array!, quoteSegment.Array!, 1u << 31, fields.WrittenCount);

        uint GetEOLFlag()
        {
            return newline switch
            {
                RecSep.CRLF => Field.IsCRLF,
                RecSep.Alternating => i % 2 == 0 ? Field.IsEOL : Field.IsCRLF,
                _ => Field.IsEOL,
            };
        }
    }

    private static ReadOnlySpan<T> GetDataset<T>(RecSep newline)
        where T : unmanaged
    {
        int i;

        using ValueStringBuilder vsb = new();

        for (i = 0; i < RecordCount; i++)
        {
            vsb.Append('0');
            vsb.Append(',');
            vsb.Append($"Test-{i % 10}");
            vsb.Append(',');
            vsb.Append('x', 19);
            vsb.Append(',');
            vsb.Append("\"quoted field\"");
            vsb.Append(',');
            vsb.Append('x', 19);
            vsb.Append(',');
            vsb.Append("\"quoted field\"");
            vsb.Append(GetNewline());
        }

        // pad with zeroes
        const int padding = 192;

        if (typeof(T) == typeof(char))
        {
            char[] buffer = new char[vsb.Length + padding];
            vsb.AsSpan().CopyTo(buffer);
            return buffer.AsSpan().Cast<char, T>();
        }
        else
        {
            Assert.True(typeof(T) == typeof(byte));
            int size = Encoding.UTF8.GetByteCount(vsb.AsSpan());
            byte[] buffer = new byte[size + padding];
            Encoding.UTF8.GetBytes(vsb.AsSpan(), buffer);
            return buffer.AsSpan().Cast<byte, T>();
        }

        string GetNewline()
        {
            return newline switch
            {
                RecSep.LF => "\n",
                RecSep.CRLF => "\r\n",
                RecSep.Alternating => i % 2 == 0 ? "\n" : "\r\n",
                _ => "\r",
            };
        }
    }

    // [Fact]
    // public void Should_Tokenize()
    // {
    //     Core<Vec128>();
    //     Core<Vec256>();
    //     Core<Vec512>();

    //     static void Core<TVector>()
    //         where TVector : struct, IAsciiVector<TVector>
    //     {
    //         BoundaryImpl<NewlineLF, TVector>("xxxxx\nyyyyy\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
    //         BoundaryImpl<NewlineCRLF, TVector>("xxxxx\nyyyyy\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
    //         BoundaryImpl<NewlineCRLF, TVector>("xxxxx\r\nyyyyy\r\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
    //     }
    // }

    // public static TheoryData<int> Primes => [11, 17, 19, 31, 37, 67, 73, 97];

    // [Theory, MemberData(nameof(Primes))]
    // public void Should_Handle_Boundary_128(int prime) => RunBoundaryCore<Vec128>(prime);

    // [Theory, MemberData(nameof(Primes))]
    // public void Should_Handle_Boundary_256(int prime) => RunBoundaryCore<Vec256>(prime);

    // [Theory, MemberData(nameof(Primes))]
    // public void Should_Handle_Boundary_512(int prime) => RunBoundaryCore<Vec512>(prime);

    // private static void RunBoundaryCore<TVector>(int iterationLength)
    //     where TVector : struct, IAsciiVector<TVector>
    // {
    //     // regular newline vector boundary checks
    //     using (ValueStringBuilder vsb = new())
    //     {
    //         string str = new string('x', iterationLength - 1);

    //         for (int i = 0; i < 1024; i++)
    //         {
    //             // ensure each iteration adds a prime length of data
    //             vsb.Append(str);
    //             vsb.Append('\n');
    //         }

    //         BoundaryImpl<NewlineLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
    //         BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
    //     }

    //     // two tokens
    //     using (ValueStringBuilder vsb = new())
    //     {
    //         string str = new string('x', iterationLength - 2);

    //         for (int i = 0; i < 1024; i++)
    //         {
    //             // ensure each iteration adds a prime length of data
    //             vsb.Append(str);
    //             vsb.Append("\r\n");
    //         }

    //         BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
    //     }

    //     // odd case #1: two LF's with CRLF parser
    //     using (ValueStringBuilder vsb = new())
    //     {
    //         string str = new string('x', iterationLength - 2);
    //         List<string> expected = [];

    //         for (int i = 0; i < 1024; i++)
    //         {
    //             // ensure each iteration adds a prime length of data
    //             vsb.Append(str);
    //             vsb.Append("\n\n");

    //             expected.Add(str);
    //             expected.Add("");
    //         }

    //         BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
    //     }

    //     // odd case #2: LF followed by delimiter with CRLF parser
    //     using (ValueStringBuilder vsb = new())
    //     {
    //         string str = new string('x', iterationLength - 3);

    //         List<string> expected = [];

    //         for (int i = 0; i < 1024; i++)
    //         {
    //             // ensure each iteration adds a prime length of data
    //             vsb.Append(str);
    //             vsb.Append(',');
    //             vsb.Append(str);
    //             vsb.Append("\n,");

    //             expected.Add(str);
    //             expected.Add(str);
    //             expected.Add("");
    //         }

    //         BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
    //     }

    //     // odd case #3: CRLF followed by delimiter with CRLF parser
    //     using (ValueStringBuilder vsb = new())
    //     {
    //         string str = new string('x', iterationLength - 4);

    //         List<string> expected = [];

    //         for (int i = 0; i < 1024; i++)
    //         {
    //             // ensure each iteration adds a prime length of data
    //             vsb.Append(str);
    //             vsb.Append(',');
    //             vsb.Append(str);
    //             vsb.Append("\r\n,");

    //             expected.Add(str);
    //             expected.Add(str);
    //             expected.Add("");
    //         }

    //         BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
    //     }
    // }

    // private static void BoundaryImpl<TNewline>(ReadOnlySpan<char> input, IEnumerable<string> expected)
    //     where TNewline : struct, INewline
    // {
    //     // get more data than needed to ensure the simd tokenizer reads to end
    //     char[] buffer = ArrayPool<char>.Shared.Rent(input.Length + (Vector256<byte>.Count * 8));
    //     buffer.AsSpan().Clear();

    //     Assert.True(
    //         Transcode.TryFromChars(input, buffer, out int length),
    //         "Failed to transcode input string to buffer."
    //     );

    //     var tokenizer = new SimdTokenizer<char, TNewline>(CsvOptions<char>.Default);

    //     Meta[] metaBuffer = ArrayPool<Meta>.Shared.Rent(4096);
    //     metaBuffer.AsSpan().Clear();
    //     int count = tokenizer.Tokenize(metaBuffer.AsSpan(1), buffer, 0);

    //     Assert.Equal(
    //         expected,
    //         metaBuffer[..count]
    //             .Select((m, i) => Transcode.ToString(buffer.AsSpan(m.NextStart..metaBuffer[i + 1].End)))
    //             .ToArray()
    //     );

    //     ArrayPool<char>.Shared.Return(buffer);
    //     ArrayPool<Meta>.Shared.Return(metaBuffer);
    // }

    // [Fact]
    // public static void AltTest()
    // {
    //     char[] buffer = new char[256];
    //     "a,b,c\nd,e,f\ng,h,i\n".CopyTo(buffer);

    //     using var rb = new RecordBuffer();
    //     var tokenizer = new SimdTokenizer<char, NewlineCRLF>(CsvOptions<char>.Default);

    //     bool read = tokenizer.Tokenize(rb, buffer);
    //     Assert.True(read, "Failed to read records from buffer.");
    //     Assert.Equal(3, rb.RecordsBuffered);

    //     Assert.True(rb.TryPop(out var segment));
    //     Assert.Equal(2, rb.RecordsBuffered);
    //     var slice = new CsvSlice<char> { Data = buffer, Fields = segment };
    //     Assert.Equal(3, slice.FieldCount);
    //     Assert.Equal("a,b,c", slice.RawValue);

    //     Assert.True(rb.TryPop(out segment));
    //     Assert.Equal(1, rb.RecordsBuffered);
    //     slice = new CsvSlice<char> { Data = buffer, Fields = segment };
    //     Assert.Equal(3, slice.FieldCount);
    //     Assert.Equal("d,e,f", slice.RawValue);

    //     Assert.True(rb.TryPop(out segment));
    //     Assert.Equal(0, rb.RecordsBuffered);
    //     slice = new CsvSlice<char> { Data = buffer, Fields = segment };
    //     Assert.Equal(3, slice.FieldCount);
    //     Assert.Equal("g,h,i", slice.RawValue);

    //     Assert.False(rb.TryPop(out _), "Expected no more records to be available.");
    // }
}
