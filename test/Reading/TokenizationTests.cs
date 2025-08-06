// #if false // TODO: tokenizer_refactor
using System.Buffers;
using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Intrinsics;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Reading;

public class TokenizationTests
{
    [Fact]
    public void Nakki()
    {
        var tokeniser = new SimdTokenizer<char, NewlineCRLF>(CsvOptions<char>.Default);
        using var buffer = new RecordBuffer();

        const string str = "aaa,bbb,ccc,ddd,eee,fff,ggg,hhh,iii,jjj,123,456,789\n";
        var data = new char[1024];
        str.CopyTo(data);

        Assert.True(tokeniser.Tokenize(buffer, data));

        Assert.Equal(str.Length, buffer.BufferedDataLength);
    }

    [Fact]
    public void Nakki2()
    {
        var tokeniser = new Avx2Tokenizer<char, NewlineLF>(CsvOptions<char>.Default);
        using var buffer = new RecordBuffer();

        const string str = "aaa,bbb,ccc,ddd,eee\nfff,ggg,hhh,iii\njjj,123,456,789\n";
        var data = new char[1024];
        str.CopyTo(data);

        Assert.True(tokeniser.Tokenize(buffer, data));

        Assert.Equal(str.Length, buffer.BufferedDataLength);
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
// #endif
