using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline
{
    /// <summary>
    /// Returns the length of the newline sequence.
    /// </summary>
    static abstract int GetLength(bool isMultitoken);

    /// <summary>
    /// Returns the offset required in the search space to be able to read the whole newline value.<br/>
    /// This is always <c>0</c> or <c>1</c>.
    /// </summary>
    static abstract nuint OffsetFromEnd { get; }

    /// <summary>
    /// Determines if the specified value is part of a two-token newline sequence.
    /// </summary>
    /// <remarks>For single token newlines, always returns false</remarks>
    static abstract bool IsMultitoken<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>;

    /// <summary>
    /// Determines if the specified value represents a delimiter or a newline.
    /// </summary>
    /// <param name="value">The value to check against</param>
    /// <param name="isMultitoken">When true, whether the next token was part of the newline as well.</param>
    /// <returns>True if the value represents a newline instead of a delimiter.</returns>
    /// <remarks>For single token newlines, always returns true</remarks>
    static abstract bool IsNewline<T>(ref T value, out bool isMultitoken)
        where T : unmanaged, IBinaryInteger<T>;

    /// <summary>
    /// Determines if the specified value represents any newline character.
    /// </summary>
    static abstract bool IsNewline<T>(T value)
        where T : unmanaged, IBinaryInteger<T>;

    /// <summary>
    /// Checks if the input vector contains a newline.
    /// </summary>
    static abstract TVector HasNewline<TVector>(TVector input)
        where TVector : struct, IAsciiVector<TVector>;
}

[SkipLocalsInit]
internal readonly struct NewlineLF : INewline
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLength(bool isMultitoken) => 1;

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultitoken<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        // single token newlines are never multitoken
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewline<T>(ref T value, out bool isMultitoken)
        where T : unmanaged, IBinaryInteger<T>
    {
        // the HasNewline vector only contains the correct values, e.g., \n, so this check should always succeed
        isMultitoken = false;

        // compared to T.CreateTruncating this type check produces 13 vs 16 bytes of code for byte (char unchanged at 14)
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Unsafe.As<T, byte>(ref value) == '\n',
            sizeof(char) => Unsafe.As<T, char>(ref value) == '\n',
            _ => throw Token<T>.NotSupported,
        };
    }

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
    public static TVector HasNewline<TVector>(TVector input)
        where TVector : struct, IAsciiVector<TVector>
    {
        return TVector.Equals(input, TVector.Create((byte)'\n'));
    }
}

[SkipLocalsInit]
internal readonly struct NewlineCRLF : INewline
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLength(bool isMultitoken) => 1 + isMultitoken.ToByte();

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultitoken<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        // only \r\n is considered a multitoken newline, other combinations e.g. \n\n are two distinct newlines

        // compared to T.CreateTruncating, this type check produces 20 bytes of code for byte.
        // no difference for char at 26, but we'll leave it in case something changes in a future .NET update
        if (Unsafe.SizeOf<T>() is sizeof(byte))
        {
            return Unsafe.As<T, byte>(ref value) == '\r' && Unsafe.Add(ref Unsafe.As<T, byte>(ref value), 1) == '\n';
        }

        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            return Unsafe.As<T, char>(ref value) == '\r' && Unsafe.Add(ref Unsafe.As<T, char>(ref value), 1) == '\n';
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewline<T>(ref T value, out bool isMultitoken)
        where T : unmanaged, IBinaryInteger<T>
    {
        isMultitoken = false;

        // this type check produces less ASM code than simple generics: 42 vs 50 for byte, 43 vs 50 for char
        if (Unsafe.SizeOf<T>() is sizeof(byte))
        {
            ref byte v = ref Unsafe.As<T, byte>(ref value);

            if (v == '\r')
            {
                // Highly predictable branch - almost always true
                isMultitoken = Unsafe.Add(ref v, 1) == '\n';
                return true;
            }
            if (v == '\n')
            {
                return true;
            }
        }
        else if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            ref char v = ref Unsafe.As<T, char>(ref value);

            if (v == '\r')
            {
                // Highly predictable branch - almost always true
                isMultitoken = Unsafe.Add(ref v, 1) == '\n';
                return true;
            }
            if (v == '\n')
            {
                return true;
            }
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        Unsafe.SkipInit(out isMultitoken); // shave off 2-3 bytes, this is never checked if returned false
        return false;
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
    public static TVector HasNewline<TVector>(TVector input)
        where TVector : struct, IAsciiVector<TVector>
    {
        return TVector.Equals(input, TVector.Create((byte)'\r')) | TVector.Equals(input, TVector.Create((byte)'\n'));
    }
}
