using System.Runtime.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public static class SimdVectorTests
{
    private const string CharData = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static ReadOnlySpan<byte> ByteData => "abcdefghijklmnopqrstuvwxyz0123456789"u8;

    [Fact]
    public static void Should_Create_Vector()
    {
        Impl<byte, Vec128Byte>(ByteData, (v, b) => ((Vector128<byte>)v).CopyTo(b));
        Impl<byte, Vec256Byte>(ByteData, (v, b) => ((Vector256<byte>)v).CopyTo(b));
        // Impl<byte, Vec512Byte>(ByteData, (v, b) => ((Vector512<byte>)v).CopyTo(b));

        Impl<char, Vec128Char>(CharData, (v, b) => ((Vector128<byte>)v).CopyTo(b));
        Impl<char, Vec256Char>(CharData, (v, b) => ((Vector256<byte>)v).CopyTo(b));
        // Impl<char, Vec512Char>(CharData, (v, b) => ((Vector512<byte>)v).CopyTo(b));
    }

    [Fact]
    public static void Should_Handle_Non_Ascii_Chars()
    {
        foreach (var c in (char[]) [',', '"', '\n', '\r', '\0', '\t', '\\'])
        {
            Impl2<Vec128Char>(c);
            Impl2<Vec256Char>(c);
            // Impl2<Vec512Char>(c);
        }

        static void Impl2<TVector>(char token) where TVector : struct, ISimdVector<char, TVector>
        {
            var data = new string(
                Enumerable
                    .Range(0, (1024 * 8 / TVector.Count) + TVector.Count)
                    .Select(i => i == token ? (char)(i + 1) : (char)i)
                    .ToArray());

            for (int i = 0; i < data.Length; i += TVector.Count)
            {
                var vec = TVector.LoadUnaligned(in data.AsSpan()[i], 0);
                var commaCheck = TVector.Create(token);

                var eq = TVector.Equals(vec, commaCheck);

                if (eq != TVector.Zero)
                {
                    char actual = data[i + (int)eq.ExtractMostSignificantBits()];
                    Assert.Fail(
                        $"Matched: {actual} ({(int)actual} / {(int)actual:X}) to {token} ({(int)token} / {(int)token:X})" +
                        Environment.NewLine +
                        $"Chars: [{data.AsSpan(i, TVector.Count)}], Bytes: " +
                        string.Join(',', data.AsSpan(i, TVector.Count).ToArray().Select(x => ((int)x).ToString("X"))) +
                        Environment.NewLine +
                        $"Vec: {vec}");
                }
            }
        }
    }

    private static void Impl<T, TVector>(ReadOnlySpan<T> data, Action<TVector, Span<byte>> toBytes)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>
    {
        if (!TVector.IsSupported) return;

        var abcVec = TVector.LoadUnaligned(in data[0], 0);
        var aVec = TVector.Create(data[0]);

        Assert.True(abcVec == TVector.LoadUnaligned(in data[0], 0));
        Assert.True(aVec == TVector.Create(data[0]));
        Assert.False(abcVec == aVec);

        byte[] bytes = new byte[TVector.Count];

        toBytes(abcVec, bytes);
        Assert.Equal(ByteData.Slice(0, bytes.Length), bytes);

        toBytes(aVec, bytes);
        Assert.Equal(Enumerable.Repeat(byte.CreateChecked(data[0]), TVector.Count).ToArray(), bytes);
    }
}
