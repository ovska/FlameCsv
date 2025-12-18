using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Utilities.Comparers;

internal sealed class IgnoreCaseAsciiComparer
    : IEqualityComparer<StringLike>,
        IEqualityComparer<string>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, string>
{
    public static IgnoreCaseAsciiComparer Instance { get; } = new();

    private IgnoreCaseAsciiComparer() { }

    public bool Equals(StringLike x, StringLike y) => Ascii.EqualsIgnoreCase(x, y);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other) => Ascii.EqualsIgnoreCase(alternate, other);

    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public int GetHashCode(StringLike obj)
    {
        return Utf8Util.WithChars(obj, this, static (obj, state) => state.GetHashCode(obj));
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        if (alternate.IsEmpty)
            return 0;

        // use a scalar impl instead of Ascii.ToLower as we expect most headers to be short

        ref byte first = ref MemoryMarshal.GetReference(alternate);
        nint index = 0;
        nint remaining = alternate.Length;

        HashCode hash = new();

        while (remaining >= sizeof(ulong))
        {
            ulong value = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref first, index));
            hash.Add(ConvertAllAsciiBytesInUInt64ToLowercase(value));
            index += 8;
            remaining -= 8;
        }

        while (remaining >= sizeof(uint))
        {
            uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref first, index));
            hash.Add(ConvertAllAsciiBytesInUInt32ToLowercase(value));
            index += 4;
            remaining -= 4;
        }

        while (remaining > 0)
        {
            byte value = Unsafe.Add(ref first, index);
            hash.Add(ConvertAsciiByteToLowercase(value));
            index++;
            remaining--;
        }

        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ConvertAllAsciiBytesInUInt64ToLowercase(ulong value)
    {
        ulong lowerIndicator = value + 0x8080_8080_8080_8080ul - 0x4141_4141_4141_4141ul;
        ulong upperIndicator = value + 0x8080_8080_8080_8080ul - 0x5B5B_5B5B_5B5B_5B5Bul;
        ulong combinedIndicator = (lowerIndicator ^ upperIndicator);
        ulong mask = (combinedIndicator & 0x8080_8080_8080_8080ul) >> 2;
        return value ^ mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ConvertAllAsciiBytesInUInt32ToLowercase(uint value)
    {
        uint lowerIndicator = value + 0x8080_8080u - 0x4141_4141u;
        uint upperIndicator = value + 0x8080_8080u - 0x5B5B_5B5Bu;
        uint combinedIndicator = (lowerIndicator ^ upperIndicator);
        uint mask = (combinedIndicator & 0x8080_8080u) >> 2;
        return value ^ mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte ConvertAsciiByteToLowercase(byte value)
    {
        byte lowerIndicator = (byte)(value + 0x80u - 0x41u);
        byte upperIndicator = (byte)(value + 0x80u - 0x5Bu);
        byte combinedIndicator = (byte)(lowerIndicator ^ upperIndicator);
        byte mask = (byte)((combinedIndicator & 0x80u) >> 2);

        return (byte)(value ^ mask);
    }

    bool IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Equals(ReadOnlySpan<byte> alternate, string other)
    {
        return Equals(alternate, (StringLike)other);
    }

    string IAlternateEqualityComparer<ReadOnlySpan<byte>, string>.Create(ReadOnlySpan<byte> alternate)
    {
        return Create(alternate);
    }

    bool IEqualityComparer<string>.Equals(string? x, string? y)
    {
        if (x is null)
        {
            return y is null;
        }

        if (y is null)
        {
            return false;
        }

        return Equals((StringLike)x, (StringLike)y);
    }

    int IEqualityComparer<string>.GetHashCode(string obj)
    {
        return GetHashCode((StringLike)obj);
    }
}
