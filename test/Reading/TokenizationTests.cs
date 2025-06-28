using System.Buffers;
using System.Text;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Reading;

public class TokenizationTests
{
    [Fact]
    public void Should_Tokenize()
    {
        Core<Vec128>();
        Core<Vec256>();
        Core<Vec512>();

        static void Core<TVector>()
            where TVector : struct, IAsciiVector<TVector>
        {
            BoundaryImpl<NewlineLF, TVector>("xxxxx\nyyyyy\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
            BoundaryImpl<NewlineCRLF, TVector>("xxxxx\nyyyyy\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
            BoundaryImpl<NewlineCRLF, TVector>("xxxxx\r\nyyyyy\r\nzzzzzz\n", ["xxxxx", "yyyyy", "zzzzzz"]);
        }
    }

    public static TheoryData<int> Primes => [11, 17, 19, 31, 37, 67, 73, 97];

    [Theory, MemberData(nameof(Primes))]
    public void Should_Handle_Boundary_128(int prime) => RunBoundaryCore<Vec128>(prime);

    [Theory, MemberData(nameof(Primes))]
    public void Should_Handle_Boundary_256(int prime) => RunBoundaryCore<Vec256>(prime);

    [Theory, MemberData(nameof(Primes))]
    public void Should_Handle_Boundary_512(int prime) => RunBoundaryCore<Vec512>(prime);

    private static void RunBoundaryCore<TVector>(int iterationLength)
        where TVector : struct, IAsciiVector<TVector>
    {
        // regular newline vector boundary checks
        using (ValueStringBuilder vsb = new())
        {
            string str = new string('x', iterationLength - 1);

            for (int i = 0; i < 1024; i++)
            {
                // ensure each iteration adds a prime length of data
                vsb.Append(str);
                vsb.Append('\n');
            }

            BoundaryImpl<NewlineLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
            BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
        }

        // two tokens
        using (ValueStringBuilder vsb = new())
        {
            string str = new string('x', iterationLength - 2);

            for (int i = 0; i < 1024; i++)
            {
                // ensure each iteration adds a prime length of data
                vsb.Append(str);
                vsb.Append("\r\n");
            }

            BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), Enumerable.Repeat(str, 1024));
        }

        // odd case #1: two LF's with CRLF parser
        using (ValueStringBuilder vsb = new())
        {
            string str = new string('x', iterationLength - 2);
            List<string> expected = [];

            for (int i = 0; i < 1024; i++)
            {
                // ensure each iteration adds a prime length of data
                vsb.Append(str);
                vsb.Append("\n\n");

                expected.Add(str);
                expected.Add("");
            }

            BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
        }

        // odd case #2: LF followed by delimiter with CRLF parser
        using (ValueStringBuilder vsb = new())
        {
            string str = new string('x', iterationLength - 3);

            List<string> expected = [];

            for (int i = 0; i < 1024; i++)
            {
                // ensure each iteration adds a prime length of data
                vsb.Append(str);
                vsb.Append(',');
                vsb.Append(str);
                vsb.Append("\n,");

                expected.Add(str);
                expected.Add(str);
                expected.Add("");
            }

            BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
        }

        // odd case #3: CRLF followed by delimiter with CRLF parser
        using (ValueStringBuilder vsb = new())
        {
            string str = new string('x', iterationLength - 4);

            List<string> expected = [];

            for (int i = 0; i < 1024; i++)
            {
                // ensure each iteration adds a prime length of data
                vsb.Append(str);
                vsb.Append(',');
                vsb.Append(str);
                vsb.Append("\r\n,");

                expected.Add(str);
                expected.Add(str);
                expected.Add("");
            }

            BoundaryImpl<NewlineCRLF, TVector>(vsb.AsSpan(), expected);
        }
    }

    private static void BoundaryImpl<TNewline, TVector>(ReadOnlySpan<char> input, IEnumerable<string> expected)
        where TNewline : struct, INewline
        where TVector : struct, IAsciiVector<TVector>
    {
        // get more data than needed to ensure the simd tokenizer reads to end
        char[] buffer = ArrayPool<char>.Shared.Rent(input.Length + (TVector.Count * 8));
        buffer.AsSpan().Clear();

        Assert.True(
            Transcode.TryFromChars(input, buffer, out int length),
            "Failed to transcode input string to buffer."
        );

        var tokenizer = new SimdTokenizer<char, TNewline, TVector>(CsvOptions<char>.Default);

        Meta[] metaBuffer = ArrayPool<Meta>.Shared.Rent(4096);
        int count = tokenizer.Tokenize(metaBuffer.AsSpan(1), buffer, 0);

        Assert.Equal(
            expected,
            metaBuffer[..count]
                .Select((m, i) => Transcode.ToString(buffer.AsSpan(m.NextStart..metaBuffer[i + 1].End)))
                .ToArray()
        );

        ArrayPool<char>.Shared.Return(buffer);
        ArrayPool<Meta>.Shared.Return(metaBuffer);
    }
}
