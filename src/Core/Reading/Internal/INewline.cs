using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace FlameCsv.Reading.Internal;

#pragma warning disable RCS1154 // Sort enum members

internal enum FieldFlag : uint
{
    None = 0,
    EOL = Field.IsEOL,
    CRLF = Field.IsCRLF,
}

#pragma warning restore RCS1154 // Sort enum members

internal interface IMaskClear
{
    static abstract void Clear(ref uint mask);
}

internal readonly struct BLSRMaskClear : IMaskClear
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(ref uint mask)
    {
        mask &= mask - 1;
    }
}

internal readonly struct LeftShiftMaskClear : IMaskClear
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(ref uint mask)
    {
        mask = (mask << 1) | 1;
    }
}

internal readonly struct IncrementMaskClear : IMaskClear
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(ref uint mask)
    {
        mask++;
    }
}

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline
{
    /// <summary>
    /// Returns whether the newline is a two-token sequence or not.
    /// </summary>
    static abstract bool IsCRLF { get; }

    static abstract FieldFlag IsNewline<T, TClear>(T delimiter, ref T value, ref uint mask)
        where T : unmanaged, IBinaryInteger<T>
        where TClear : struct, IMaskClear;
}

[SkipLocalsInit]
internal readonly struct NewlineLF : INewline
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldFlag GetFlag<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T> => FieldFlag.EOL;

    public static bool IsCRLF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldFlag IsNewline<T, TClear>(T delimiter, ref T value, ref uint mask)
        where T : unmanaged, IBinaryInteger<T>
        where TClear : struct, IMaskClear
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
            sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
            _ => throw Token<T>.NotSupported,
        }
            ? FieldFlag.EOL
            : FieldFlag.None;
    }
}

[SkipLocalsInit]
internal readonly struct NewlineCRLF : INewline
{
    public static bool IsCRLF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldFlag IsNewline<T, TClear>(T delimiter, ref T value, ref uint mask)
        where T : unmanaged, IBinaryInteger<T>
        where TClear : struct, IMaskClear
    {
        FieldFlag retVal = 0;

        if (delimiter != value)
        {
            bool isCRLF;

            if (Unsafe.SizeOf<T>() is sizeof(byte))
            {
                ushort crlf = MemoryMarshal.Read<ushort>("\r\n"u8);
                isCRLF = Unsafe.As<T, ushort>(ref value) == crlf;
            }
            else if (Unsafe.SizeOf<T>() is sizeof(char))
            {
                uint crlf = MemoryMarshal.Read<uint>(MemoryMarshal.Cast<char, byte>("\r\n"));
                isCRLF = Unsafe.As<T, uint>(ref value) == crlf;
            }
            else
            {
                throw Token<T>.NotSupported;
            }

            if (isCRLF)
            {
                retVal = FieldFlag.CRLF;
                TClear.Clear(ref mask);
            }
            else
            {
                retVal = FieldFlag.EOL;
            }
        }

        return retVal;
    }
}
