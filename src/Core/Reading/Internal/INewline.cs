using System.Runtime.CompilerServices;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline
{
    /// <summary>
    /// Returns whether the newline is a two-token sequence or not.
    /// </summary>
    static abstract bool IsCRLF { get; }

    static abstract uint GetNewlineFlag<T>(T delimiter, ref T value)
        where T : unmanaged, IBinaryInteger<T>;
}

[SkipLocalsInit]
internal readonly struct NewlineLF : INewline
{
    public static bool IsCRLF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetNewlineFlag<T>(T delimiter, ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
            sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
            _ => throw Token<T>.NotSupported,
        }
            ? Field.IsEOL
            : 0;
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
    public static uint GetNewlineFlag<T>(T delimiter, ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        uint retVal = 0;

        if (delimiter != value)
        {
            if (Bithacks.IsCRLF(ref value))
            {
                retVal = Field.IsCRLF;
            }
            else
            {
                retVal = Field.IsEOL;
            }
        }

        return retVal;
    }
}
