using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv.Extensions;

internal static class WriteExtensions
{
    /// <summary>
    /// Attempts to copy the contents of <paramref name="value"/> to <paramref name="buffer"/>.
    /// </summary>
    /// <param name="value">Value to copy, can be empty</param>
    /// <param name="buffer">Destination buffer</param>
    /// <param name="tokensWritten">Length of <paramref name="value"/> if the copy succeeded</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>True if the destination buffer is large enough and data was copied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCopyTo<T>(this ReadOnlySpan<T> value, Span<T> buffer, out int tokensWritten)
    {
        if (value.TryCopyTo(buffer))
        {
            tokensWritten = value.Length;
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    public static bool TryTranscodeTo<T>(this ReadOnlySpan<T> value, Span<char> buffer, out int charsWritten)
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

    public static string TranscodeToString<T>(this ReadOnlySpan<T> value)
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
