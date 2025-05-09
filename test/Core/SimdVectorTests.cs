using FlameCsv.Intrinsics;

namespace FlameCsv.Tests;

public static class SimdVectorTests
{
    // TODO: create a script that runs these tests with all possible instruction combinations (e.g. AVX2 disabled)

    private const string CharData = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static ReadOnlySpan<byte> ByteData =>
        "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"u8;

    [Fact]
    public static void Vector_128_Byte() => Test<byte, Vec128Byte>(ByteData);

    [Fact]
    public static void Vector_256_Byte() => Test<byte, Vec256Byte>(ByteData);

    [Fact]
    public static void Vector_512_Byte() => Test<byte, Vec512Byte>(ByteData);

    [Fact]
    public static void Vector_128_Char() => Test<char, Vec128Char>(CharData);

    [Fact]
    public static void Vector_256_Char() => Test<char, Vec256Char>(CharData);

    [Fact]
    public static void Vector_512_Char() => Test<char, Vec512Char>(CharData);

    [Fact]
    public static void NonAscii_128() => TestNonAscii<Vec128Char>();

    [Fact]
    public static void NonAscii_256() => TestNonAscii<Vec256Char>();

    [Fact]
    public static void NonAscii_512() => TestNonAscii<Vec512Char>();

    private static void Test<T, TVector>(ReadOnlySpan<T> data)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>
    {
        Assert.SkipUnless(TVector.IsSupported, $"CPU support not available for {typeof(TVector).Name}");

        // loading and equality (also tests narrowing in char-data)
        var abcVec = TVector.LoadUnaligned(in data[0], 0);
        var aVec = TVector.Create(data[0]);

        Assert.True(abcVec == TVector.LoadUnaligned(in data[0], 0));
        Assert.True(aVec == TVector.Create(data[0]));
        Assert.False(abcVec == aVec);
        Assert.Equal(ByteData.Slice(0, TVector.Count), abcVec.ToArray());
        Assert.Equal(Enumerable.Repeat(byte.CreateChecked(data[0]), TVector.Count).ToArray(), aVec.ToArray());
    }

    /// <summary>
    /// Test for no clashes even if non-ASCII characters are in the data.
    /// </summary>
    private static void TestNonAscii<TVector>()
        where TVector : struct, ISimdVector<char, TVector>
    {
        Assert.SkipUnless(TVector.IsSupported, $"CPU support not available for {typeof(TVector).Name}");

        ReadOnlySpan<char> tokens = [',', '"', '\n', '\r', '\0', '\t', '\\'];

        foreach (var token in tokens)
        {
            Assert.True(char.IsAscii(token));

            var data = new string(
                Enumerable
                    .Range(0, (1024 * 8 / TVector.Count) + TVector.Count)
                    .Select(i => i == token ? (char)(i + 1) : (char)i)
                    .ToArray()
            );

            for (int i = 0; i < data.Length; i += TVector.Count)
            {
                var vec = TVector.LoadUnaligned(in data.AsSpan()[i], 0);
                var commaCheck = TVector.Create(token);

                var eq = TVector.Equals(vec, commaCheck);

                if (eq != TVector.Zero)
                {
                    char actual = data[i + (int)eq.ExtractMostSignificantBits()];
                    Assert.Fail(
                        $"Matched: {actual} ({(int)actual} / {(int)actual:X}) to {token} ({(int)token} / {(int)token:X})"
                            + Environment.NewLine
                            + $"Chars: [{data.AsSpan(i, TVector.Count)}], Bytes: "
                            + string.Join(
                                ',',
                                data.AsSpan(i, TVector.Count).ToArray().Select(x => ((int)x).ToString("X"))
                            )
                            + Environment.NewLine
                            + $"Vec: {vec}"
                    );
                }
            }
        }

        // ensure narrowing saturates or zeroes out, instead of just discarding high bits
        {
            const char weirdComma = (char)(',' | ',' << 8);
            Span<char> span = stackalloc char[TVector.Count];
            span.Fill(weirdComma);
            TVector vec = TVector.LoadUnaligned(in span[0], 0);
            var commaCheck = TVector.Create(',');
            var eq = TVector.Equals(vec, commaCheck);
            Assert.True(eq == TVector.Zero, $"Matched: {vec} to {commaCheck}");
        }
    }
}
