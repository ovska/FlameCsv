using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv;

internal static class Transcode
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxTranscodedSize<T>(scoped ReadOnlySpan<char> value)
        where T : unmanaged
    {
        if (typeof(byte) == typeof(T))
        {
            return Encoding.UTF8.GetMaxByteCount(value.Length);
        }

        if (typeof(char) == typeof(T))
        {
            return value.Length;
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
            if (value.TryCopyTo(MemoryMarshal.Cast<T, char>(destination)))
            {
                charsWritten = value.Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, MemoryMarshal.AsBytes(destination), out charsWritten);
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToChars<T>(ReadOnlySpan<T> value, Span<char> buffer, out int charsWritten)
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            if (value.TryCopyTo(MemoryMarshal.Cast<char, T>(buffer)))
            {
                charsWritten = value.Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(MemoryMarshal.AsBytes(value), buffer, out charsWritten);
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> FromString<T>(string? value)
        where T : unmanaged
    {
        if (typeof(T) == typeof(char))
        {
            var mem = value.AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref mem);
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
            return Encoding.UTF8.GetString(MemoryMarshal.AsBytes(value));
        }

        throw Token<T>.NotSupported;
    }
}
