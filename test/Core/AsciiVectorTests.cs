using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public static class AsciiVectorTests
{
    // TODO: create a script that runs these tests with all possible instruction combinations (e.g. AVX2 disabled)

    private const string CharData = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static ReadOnlySpan<byte> ByteData =>
        "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"u8;

    [Fact]
    public static void Vector_128_Byte() => Test<byte, Vec128>(ByteData);

    [Fact]
    public static void Vector_256_Byte() => Test<byte, Vec256>(ByteData);

    [Fact]
    public static void Vector_512_Byte() => Test<byte, Vec512>(ByteData);

    [Fact]
    public static void Vector_128_Char() => Test<char, Vec128>(CharData);

    [Fact]
    public static void Vector_256_Char() => Test<char, Vec256>(CharData);

    [Fact]
    public static void Vector_512_Char() => Test<char, Vec512>(CharData);

    [Fact]
    public static void NonAscii_128() => TestNonAscii<Vec128>();

    [Fact]
    public static void NonAscii_256() => TestNonAscii<Vec256>();

    [Fact]
    public static void NonAscii_512() => TestNonAscii<Vec512>();

    private static void Test<T, TVector>(ReadOnlySpan<T> dataROS)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, IAsciiVector<TVector>
    {
        Assert.SkipUnless(TVector.IsSupported, $"CPU support not available for {typeof(TVector).Name}");

        Span<T> data = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in dataROS[0]), dataROS.Length);

        // loading and equality (also tests narrowing in char-data)
        var abcVec = TVector.LoadUnaligned(ref data[0], 0);
        var aVec = TVector.Create(data[0]);

        Assert.True(abcVec == TVector.LoadUnaligned(ref data[0], 0));
        Assert.True(aVec == TVector.Create(data[0]));
        Assert.False(abcVec == aVec);
        Assert.Equal(ByteData.Slice(0, TVector.Count), abcVec.ToArray());
        Assert.Equal(Enumerable.Repeat(byte.CreateChecked(data[0]), TVector.Count).ToArray(), aVec.ToArray());

        Assert.Throws<NotSupportedException>(() => TVector.Create<int>(0));
    }

    /// <summary>
    /// Test for no clashes even if non-ASCII characters are in the data.
    /// </summary>
    private static void TestNonAscii<TVector>()
        where TVector : struct, IAsciiVector<TVector>
    {
        Assert.SkipUnless(TVector.IsSupported, $"CPU support not available for {typeof(TVector).Name}");

        ReadOnlySpan<char> tokens = [',', '"', '\n', '\r', '\0', '\t', '\\'];

        foreach (var token in tokens)
        {
            Assert.True(char.IsAscii(token));

            var data = Enumerable
                .Range(0, (1024 * 8 / TVector.Count) + TVector.Count)
                .Select(i => i == token ? (char)(i + 1) : (char)i)
                .ToArray();

            for (int i = 0; i < data.Length; i += TVector.Count)
            {
                var vec = TVector.LoadUnaligned(ref data[i], 0);
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

        Span<char> span = stackalloc char[TVector.Count];

        // ensure narrowing saturates or zeroes out, instead of just discarding high bits
        foreach (char control in (char[])[',', '"', '\n', '\r', '\t', '\\'])
        {
            char weirdComma = (char)(control | control << 8);
            span.Fill(weirdComma);
            TVector vec = TVector.LoadUnaligned(ref span[0], 0);
            TVector commaCheck = TVector.Create(control);
            TVector eq = TVector.Equals(vec, commaCheck);
            Assert.True(eq == TVector.Zero, $"Matched: {vec} to {commaCheck}");
        }
    }

    [Fact]
    public static void Should_Zero_Lower_128()
    {
        Assert.SkipUnless(Vector128.IsHardwareAccelerated, "Vector128 not supported");

        Span<byte> actual = stackalloc byte[Vector128<byte>.Count];
        Span<byte> expected = stackalloc byte[Vector128<byte>.Count];

        for (int i = 0; i < Vector128<byte>.Count; i++)
        {
            Vector128<byte> result = AsciiVector.ZeroLower2(Vector128<byte>.AllBitsSet, i);
            result.CopyTo(actual);

            expected.Clear();
            expected.Slice(i).Fill((byte)0xFF);

            Assert.Equal(expected, actual);
        }
    }
}
