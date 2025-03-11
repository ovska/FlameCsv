using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Tests.Readers;

public static class UnescaperTests
{
    public static TheoryData<string, string> UnescapeData
        => new()
        {
            { "\"\"", "\"" },
            { "\"\"\"\"", "\"\"" },
            { "te\"\"st", "te\"st" },
            { "\0\0\"\"\0\0", "\0\0\"\0\0" },
            { "James \"\"007\"\" Bond", "James \"007\" Bond" },
            { "James \"\"\"\"007\"\"\"\" Bond", "James \"\"007\"\" Bond" },
            { "James \"\"\"\"\"\"007\"\"\"\"\"\" Bond", "James \"\"\"007\"\"\" Bond" },
            { "James \"\"\"\"\"\"\"\"007\"\"\"\"\"\"\"\" Bond", "James \"\"\"\"007\"\"\"\" Bond" },
            { "\"\"\"\"\"\"\"\"\"\"\"\"\"\"\"\"", "\"\"\"\"\"\"\"\"" },
            { "012345678901234\"\"\"\"", "012345678901234\"\"" },
            { "012345678901234\"\"\"\"zzzzzzzzzzzzzzzzzzzzzzzzz", "012345678901234\"\"zzzzzzzzzzzzzzzzzzzzzzzzz" },
            {
                "The quick \"\"brown\"\" fox jumps over the \"\"lazy\"\" dog",
                "The quick \"brown\" fox jumps over the \"lazy\" dog"
            },
            // Odd case: firstlength is 1 under vector length, and the first vector ends in two quotes
            // this will cause the first to be shifted away, while the second will be added to carry,
            // but not ignored because it was shifted back
            {
                "Wilson Jones 1\"\" Hanging DublLock\u00ae Ring Binders",
                "Wilson Jones 1\" Hanging DublLock\u00ae Ring Binders"
            },
        };

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Compress_Byte16(string input, string output)
    {
        Assert.True(ByteSsse3Unescaper.IsSupported);

        byte[] data = Encoding.UTF8.GetBytes(input);

        var quoteCount = data.AsSpan().Count((byte)'"');
        Assert.NotEqual(0, quoteCount);
        Assert.Equal(0, quoteCount % 2);

        int requiredSize = Unescaper.GetBufferLength<ByteSsse3Unescaper>(data.Length);
        byte[] buffer = new byte[ByteSsse3Unescaper.Count + requiredSize + ByteSsse3Unescaper.Count];

        Span<byte> destination = buffer.AsSpan(ByteSsse3Unescaper.Count..^ByteSsse3Unescaper.Count);

        int compressedLength
            = Unescaper.Unescape<byte, ushort, Vector128<byte>, ByteSsse3Unescaper>((byte)'"', data, destination);

        Assert.Equal(data.Length - (quoteCount / 2), compressedLength);
        Assert.Equal(output, Encoding.UTF8.GetString(destination[..compressedLength]));

        // assert the preceding and following data buffer is all zeroes so we haven't read out of bounds
        Assert.All(buffer[..ByteSsse3Unescaper.Count], b => Assert.Equal(0, b));
        Assert.All(buffer[(ByteSsse3Unescaper.Count + requiredSize)..], b => Assert.Equal(0, b));
    }

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Compress_Byte32(string input, string output)
    {
        Assert.True(ByteAvx2Unescaper.IsSupported);

        byte[] data = Encoding.UTF8.GetBytes(input);

        var quoteCount = data.AsSpan().Count((byte)'"');
        Assert.NotEqual(0, quoteCount);
        Assert.Equal(0, quoteCount % 2);

        int requiredSize = Unescaper.GetBufferLength<ByteAvx2Unescaper>(data.Length);
        byte[] buffer = new byte[ByteAvx2Unescaper.Count + requiredSize + ByteAvx2Unescaper.Count];

        Span<byte> destination = buffer.AsSpan(ByteAvx2Unescaper.Count..^ByteAvx2Unescaper.Count);

        int compressedLength
            = Unescaper.Unescape<byte, uint, Vector256<byte>, ByteAvx2Unescaper>((byte)'"', data, destination);

        Assert.Equal(data.Length - (quoteCount / 2), compressedLength);
        Assert.Equal(output, Encoding.UTF8.GetString(destination[..compressedLength]));

        // assert the preceding and following data buffer is all zeroes so we haven't read out of bounds
        Assert.All(buffer[..ByteAvx2Unescaper.Count], b => Assert.Equal(0, b));
        Assert.All(buffer[(ByteAvx2Unescaper.Count + requiredSize)..], b => Assert.Equal(0, b));
    }

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Compress_Char8(string data, string output)
    {
        Assert.True(CharAvxUnescaper.IsSupported);

        var quoteCount = data.AsSpan().Count('"');
        Assert.NotEqual(0, quoteCount);
        Assert.Equal(0, quoteCount % 2);

        int requiredSize = Unescaper.GetBufferLength<CharAvxUnescaper>(data.Length);
        char[] buffer = new char[CharAvxUnescaper.Count + requiredSize + CharAvxUnescaper.Count];

        Span<char> destination = buffer.AsSpan(CharAvxUnescaper.Count..^CharAvxUnescaper.Count);

        int compressedLength
            = Unescaper.Unescape<char, ushort, Vector256<short>, CharAvxUnescaper>('"', data, destination);

        Assert.Equal(data.Length - (quoteCount / 2), compressedLength);
        Assert.Equal(output, new string(destination[..compressedLength]));

        // assert the preceding and following data buffer is all zeroes so we haven't read out of bounds
        Assert.All(buffer[..CharAvxUnescaper.Count], b => Assert.Equal(0, b));
        Assert.All(buffer[(CharAvxUnescaper.Count + requiredSize)..], b => Assert.Equal(0, b));
    }

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Unescape_Char_Vec128(string data, string output) => Run<char, Vec128Char>(data, output);

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Unescape_Char_Vec256(string data, string output) => Run<char, Vec256Char>(data, output);

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Unescape_Byte_Vec128(string data, string output)
        => Run<byte, Vec128Byte>(Encoding.UTF8.GetBytes(data), output);

    [Theory]
    [MemberData(nameof(UnescapeData))]
    public static void Should_Unescape_Byte_Vec256(string data, string output)
        => Run<byte, Vec256Byte>(Encoding.UTF8.GetBytes(data), output);

    private static void Run<T, TVector>(ReadOnlySpan<T> data, string output)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>
    {
        Assert.True(TVector.IsSupported);

        var quoteCount = data.Count(T.CreateChecked('"'));
        Assert.NotEqual(0, quoteCount);
        Assert.Equal(0, quoteCount % 2);

        int unescapedLength = data.Length - (quoteCount / 2);
        T[] buffer = new T[TVector.Count + unescapedLength + TVector.Count];

        RFC4180Mode<T>.Unescape<TVector>(T.CreateChecked('"'), buffer.AsSpan(TVector.Count), data, (uint)quoteCount);

        Assert.Equal(output, ((ReadOnlySpan<T>)buffer).Slice(TVector.Count, unescapedLength).AsPrintableString());

        // assert the preceding and following data buffer is all zeroes so we haven't read out of bounds
        Assert.All(buffer[..TVector.Count], b => Assert.Equal(T.Zero, b));
        Assert.All(buffer[(TVector.Count + unescapedLength)..], b => Assert.Equal(T.Zero, b));
    }
}
