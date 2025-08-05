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

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline
{
    /// <summary>
    /// Returns whether the newline is a two-token sequence or not.
    /// </summary>
    static abstract bool IsCRLF { get; }

    /// <summary>
    /// Determines if the specified value represents any newline character.
    /// </summary>
    static abstract bool IsNewline<T>(T value)
        where T : unmanaged, IBinaryInteger<T>;

    static abstract FieldFlag IsNewline<T, TMask>(T delimiter, ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>;

    static abstract FieldFlag GetKnownNewlineFlag<T, TMask>(ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>;
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
    public static FieldFlag GetKnownNewlineFlag<T, TMask>(ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        // marginal improvement over ?: return by loading the common case into the register already
        FieldFlag retVal = FieldFlag.None;

        if (
            Unsafe.SizeOf<T>() switch
            {
                sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
                sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
                _ => throw Token<T>.NotSupported,
            }
        )
        {
            retVal = FieldFlag.EOL;
        }

        return retVal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewline<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
            sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldFlag IsNewline<T, TMask>(T delimiter, ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        // TODO: profile if branch wins
        bool isLF = Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
            sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
            _ => throw Token<T>.NotSupported,
        };

        FieldFlag flag = FieldFlag.None;

        if (isLF)
        {
            flag = FieldFlag.EOL;
        }

        return flag;
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

    public static bool IsNewline<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\r' or (byte)'\n',
            sizeof(char) => Unsafe.BitCast<T, char>(value) is '\r' or '\n',
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldFlag IsNewline<T, TMask>(T delimiter, ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        FieldFlag retVal = 0;

        if (delimiter != value)
        {
            bool isCRLF;

            if (Unsafe.SizeOf<T>() is sizeof(byte))
            {
                isCRLF = Unsafe.As<T, ushort>(ref value) == MemoryMarshal.Read<ushort>("\r\n"u8);
            }
            else if (Unsafe.SizeOf<T>() is sizeof(char))
            {
                isCRLF =
                    Unsafe.As<T, uint>(ref value) == MemoryMarshal.Read<uint>(MemoryMarshal.Cast<char, byte>("\r\n"));
            }
            else
            {
                throw Token<T>.NotSupported;
            }

            if (isCRLF)
            {
                retVal = FieldFlag.CRLF;
                mask &= (mask - TMask.One); // lowered to blsr by jit
            }
            else
            {
                retVal = FieldFlag.EOL;
            }
        }

        return retVal;
    }

    public static FieldFlag GetKnownNewlineFlag<T, TMask>(ref T value, ref TMask mask)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        FieldFlag retVal;
        bool isCRLF;

        if (Unsafe.SizeOf<T>() is sizeof(byte))
        {
            isCRLF = Unsafe.As<T, ushort>(ref value) == MemoryMarshal.Read<ushort>("\r\n"u8);
        }
        else if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            isCRLF = Unsafe.As<T, uint>(ref value) == MemoryMarshal.Read<uint>(MemoryMarshal.Cast<char, byte>("\r\n"));
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        if (isCRLF)
        {
            retVal = FieldFlag.CRLF;
            mask &= (mask - TMask.One);
        }
        else
        {
            retVal = FieldFlag.EOL;
        }

        return retVal;
    }
}
