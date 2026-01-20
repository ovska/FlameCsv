using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv;

internal static class Transcode
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxTranscodedSize<T>(int length)
        where T : unmanaged
    {
        if (typeof(byte) == typeof(T))
        {
            return Encoding.UTF8.GetMaxByteCount(length);
        }

        if (typeof(char) == typeof(T))
        {
            return length;
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFromChars<T>(
        scoped ReadOnlySpan<char> value,
        scoped Span<T> destination,
        out int charsWritten
    )
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            if (value.TryCopyTo(Unsafe.BitCast<Span<T>, Span<char>>(destination)))
            {
                charsWritten = value.Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, Unsafe.BitCast<Span<T>, Span<byte>>(destination), out charsWritten);
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToChars<T>(ReadOnlySpan<T> value, Span<char> buffer, out int charsWritten)
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            charsWritten = value.Length;
            return value.TryCopyTo(Unsafe.BitCast<Span<char>, Span<T>>(buffer));
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(
                Unsafe.BitCast<ReadOnlySpan<T>, ReadOnlySpan<byte>>(value),
                buffer,
                out charsWritten
            );
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> FromString<T>(string? value)
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            return Unsafe.BitCast<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(value.AsMemory());
        }

        if (typeof(T) == typeof(byte))
        {
            return string.IsNullOrEmpty(value) ? default : Unsafe.As<T[]>(Encoding.UTF8.GetBytes(value));
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToString<T>(this ReadOnlySpan<T> value)
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            return value.ToString();
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.GetString(Unsafe.BitCast<ReadOnlySpan<T>, ReadOnlySpan<byte>>(value));
        }

        throw Token<T>.NotSupported;
    }
}
