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
    public static bool TryWriteTo<T>(
        this Span<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (buffer.Length >= value.Length)
        {
            if ((tokensWritten = value.Length) != 0)
                value.CopyTo(buffer);
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    /// <inheritdoc cref="TryWriteTo{T}(System.Span{T},System.Span{T},out int)"/>
    public static bool TryWriteTo<T>(
        this ReadOnlySpan<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (buffer.Length >= value.Length)
        {
            if ((tokensWritten = value.Length) != 0)
                value.CopyTo(buffer);
            return true;
        }

        tokensWritten = 0;
        return false;
    }
}
