using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

/// <summary>
/// Regression tests for CRLF handling in the SIMD tokenizers.
/// </summary>
public static class TokenizerCrlfTests
{
    private const string Header = "Id,Color,MaybeColor,Perms,Date,MaybeDate,Time,Big,Half,BigInt,Code,MaybeCode,MaybeGuid";

    private const string Row =
        "1,Blue,Green,Read, Write,11/07/2026,01/02/2000,13:37,170141183460469231731687303715884105727,"
        + "1.5,1000000000000000000000000000000,42,-7,6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    public static TheoryData<Type, bool> Data() =>
        [.. from type in new Tokenizers.Types() from utf8 in GlobalData.Booleans select (type, utf8)];

    [Theory, MemberData(nameof(Data))]
    public static void Should_Strip_CR_From_Unquoted_CRLF(Type type, bool utf8)
    {
        // the original repro: no quotes anywhere in the data
        string csv = Header + "\r\n" + string.Concat(Enumerable.Repeat(Row + "\r\n", 8));

        if (utf8)
            AssertSameAsScalar<byte>(type, Encoding.UTF8.GetBytes(csv));
        else
            AssertSameAsScalar<char>(type, csv.ToCharArray());
    }

    private static void AssertSameAsScalar<T>(Type type, T[] data)
        where T : unmanaged, IBinaryInteger<T>
    {
        var options = new CsvOptions<T> { Newline = CsvNewline.CRLF };
        var scalar = options.GetTokenizers().scalar;
        var simd = Tokenizers.GetTokenizer<T>(type, options);

        using RecordBuffer rbScalar = new();
        using RecordBuffer rbSimd = new();

        Span<uint> fbScalar = rbScalar.GetUnreadBuffer(0, out int scalarStart);
        Span<uint> fbSimd = rbSimd.GetUnreadBuffer(0, out int simdStart);

        int countScalar = scalar.Tokenize(fbScalar, scalarStart, data, readToEnd: true);
        int countSimd = simd.Tokenize(fbSimd, simdStart, data);

        Assert.True(countSimd > 0, "SIMD tokenizer read no fields");
        Assert.True(countScalar >= countSimd);
        Assert.Equal(Format(fbScalar[..countSimd]), Format(fbSimd[..countSimd]));
    }

    // show index, end and flags so a mismatch is readable
    private static string[] Format(ReadOnlySpan<uint> fields)
    {
        var result = new string[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            uint f = fields[i];
            result[i] = $"#{i} end={f & Field.EndMask} eol={(f & Field.IsEOL) != 0} crlf={(f & Field.IsCRLF) == Field.IsCRLF}";
        }

        return result;
    }
}
