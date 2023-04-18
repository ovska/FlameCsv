using System.Runtime.CompilerServices;
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
    public static bool TryWriteTo<T>(
        this Span<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (value.TryCopyTo(buffer))
        {
            tokensWritten = value.Length;
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    /// <inheritdoc cref="TryWriteTo{T}(Span{T},Span{T},out int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteTo<T>(
        this ReadOnlySpan<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (value.TryCopyTo(buffer))
        {
            tokensWritten = value.Length;
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    public static bool TryWriteUtf8To(
         this ReadOnlySpan<char> value,
         Span<byte> destination,
         out int tokensWritten)
    {
        if (Encoding.UTF8.GetMaxByteCount(value.Length) <= destination.Length ||
            Encoding.UTF8.GetByteCount(value) <= destination.Length)
        {
            tokensWritten = Encoding.UTF8.GetBytes(value, destination);
            return true;
        }

        tokensWritten = 0;
        return false;
    }
}
