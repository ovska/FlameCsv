using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv.Utilities.Comparers;

internal sealed class IgnoreCaseAsciiComparer
    : IEqualityComparer<StringLike>, IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>
{
    public static IgnoreCaseAsciiComparer Instance { get; } = new();

    private IgnoreCaseAsciiComparer()
    {
    }

    public bool Equals(StringLike x, StringLike y) => Ascii.EqualsIgnoreCase(x, y);

    public bool Equals(ReadOnlySpan<byte> alternate, StringLike other) => Ascii.EqualsIgnoreCase(alternate, other);

    [ExcludeFromCodeCoverage]
    public StringLike Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public int GetHashCode(StringLike obj)
    {
        return Utf8Util.WithChars(
            obj,
            this,
            static (obj, state) => state.GetHashCode(obj));
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        if (alternate.IsEmpty) return 0;

        ref byte first = ref MemoryMarshal.GetReference(alternate);
        nint index = 0;
        nint remaining = alternate.Length;

        HashCode hash = new();

        while (remaining >= sizeof(ulong))
        {
            ulong value = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref first, index));
            value = ConvertAllAsciiBytesInUInt64ToLowercase(value);
            hash.Add((int)(uint)value);
            hash.Add((int)(uint)(value >> 32));
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
}
