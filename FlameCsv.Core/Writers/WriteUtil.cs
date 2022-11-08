using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writers;

internal static class WriteUtil
{
    public static bool TryWriteEscaped<T, TValue>(
        Span<T> destination,
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        in CsvTokens<T> tokens)
        where T : unmanaged, IEquatable<T>
    {
        if (!formatter.TryFormat(value, destination, out int tokensWritten))
            return false;

        var written = destination[..tokensWritten];

        if (NeedsEscaping(written, tokens.StringDelimiter, out int quoteCount, out int escapedLength) ||
            (!tokens.Whitespace.IsEmpty && NeedsEscaping(written, tokens.Whitespace.Span, out escapedLength)))
        {
            if (destination.Length >= escapedLength)
            {
                // Escape(written, destination
            }
        }

        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsEscaping<T>(
        ReadOnlySpan<T> value,
        T quote,
        out int quoteCount,
        out int escapedLength)
        where T : unmanaged, IEquatable<T>
    {
        quoteCount = value.Count(quote);

        if (quoteCount > 0)
        {
            // original value + wrapping quotes + another for each inner quote
            escapedLength = value.Length + 2 + quoteCount;
            return true;
        }

        escapedLength = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsEscaping<T>(
        ReadOnlySpan<T> value,
        ReadOnlySpan<T> whitespace,
        out int escapedLength)
        where T : unmanaged, IEquatable<T>
    {
        if (value.Length != value.Trim(whitespace).Length)
        {
            // only the wrapping quotes needed
            escapedLength = value.Length + 2;
            return true;
        }

        escapedLength = default;
        return false;
    }

    public static void Escape<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        // Work backwards as the source and destination buffers might overlap
        // if the write buffer is large enough for the unescaped version

        int dstIndex = destination.Length - 1;
        int srcIndex = source.Length - 1;

        destination[dstIndex--] = quote;

        while (quoteCount > 0)
        {
            if (quote.Equals(source[srcIndex]))
            {
                destination[dstIndex--] = quote;
                quoteCount--;
            }

            destination[dstIndex--] = source[srcIndex--];
        }

        source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
        destination[0] = quote;
    }
}
