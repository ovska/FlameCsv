using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writers;

internal static class WriteUtil
{
    public static bool TryWriteEscaped<T, TValue>(
        Span<T> destination,
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        in CsvTokens<T> tokens,
        ref T[]? array,
        out int tokensWritten,
        out int overflowWritten)
        where T : unmanaged, IEquatable<T>
    {
        if (!formatter.TryFormat(value, destination, out tokensWritten))
        {
            overflowWritten = 0;
            return false;
        }

        if (tokensWritten > 0)
        {
            var written = destination[..tokensWritten];

            if (NeedsEscaping(written, tokens.StringDelimiter, out int quoteCount, out int escapedLength)
                || NeedsEscaping(written, tokens.Whitespace, out escapedLength))
            {
                if (destination.Length < escapedLength)
                {
                    PartialEscape(
                        written,
                        destination,
                        tokens.StringDelimiter,
                        escapedLength,
                        quoteCount,
                        ref array,
                        out overflowWritten);
                    return true;
                }

                Escape(written, destination, tokens.StringDelimiter, quoteCount);
                tokensWritten = escapedLength;
            }
        }

        overflowWritten = 0;
        return true;
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
        ReadOnlyMemory<T> whitespace,
        out int escapedLength)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!value.IsEmpty);

        if (!whitespace.IsEmpty)
        {
            var head = value[0];
            var tail = value[^1];
            var span = whitespace.Span;

            if ((span.Length == 1 && StartsOrEndsWith(head, tail, span[0]))
                || (span.Length == 2 && StartsOrEndsWith(head, tail, span[0], span[1]))
                || (span.Length == 3 && StartsOrEndsWith(head, tail, span[0], span[1], span[2]))
                || StartsOrEndsWith(head, tail, span))
            {
                // only the wrapping quotes needed
                escapedLength = value.Length + 2;
                return true;
            }
        }

        escapedLength = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsOrEndsWith<T>(T head, T tail, T a0)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head) || a0.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsOrEndsWith<T>(T head, T tail, T a0, T a1)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head)
            || a0.Equals(tail)
            || a1.Equals(head)
            || a1.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsOrEndsWith<T>(T head, T tail, T a0, T a1, T a2)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head)
            || a0.Equals(tail)
            || a1.Equals(head)
            || a1.Equals(tail)
            || a2.Equals(head)
            || a2.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsOrEndsWith<T>(T head, T tail, ReadOnlySpan<T> span)
        where T : unmanaged, IEquatable<T>
    {
        foreach (var a in span)
        {
            if (a.Equals(head) || a.Equals(tail))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="quote">Quote token</param>
    /// <param name="requiredLength">Total length required for the unescape</param>
    /// <param name="quoteCount">Amount of quotes in the source</param>
    /// <param name="array">Pooled buffer used to write the rest of the data</param>
    /// <param name="overflowLength">Amount of bytes written to the start of <paramref name="array"/></param>
    /// <typeparam name="T"></typeparam>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void PartialEscape<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int requiredLength,
        int quoteCount,
        ref T[]? array,
        out int overflowLength)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(destination.Length < requiredLength, "Destination should be too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // TODO: should this even be here? can be calculated outside
        overflowLength = requiredLength - destination.Length;
        Debug.Assert(overflowLength + destination.Length == requiredLength);

        ArrayPool<T>.Shared.EnsureCapacity(ref array, overflowLength);

        // First write the overflowing data to the array
        // as in the other Escape impl, we work backwards as source and destination
        // share a memory region
        int srcIndex = source.Length - 1;
        int ovrIndex = overflowLength - 1;
        int dstIndex = destination.Length - 1;
        bool needQuote = false;

        Span<T> overflow = array;
        overflow[ovrIndex--] = quote;

        if (quoteCount == 0)
        {
            goto CopyTo;
        }

        while (ovrIndex >= 0)
        {
            if (needQuote)
            {
                overflow[ovrIndex--] = quote;
                needQuote = false;

                if (--quoteCount == 0)
                    goto CopyTo;
            }
            else if (source[srcIndex].Equals(quote))
            {
                overflow[ovrIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                overflow[ovrIndex--] = source[srcIndex--];
            }
        }

        while (srcIndex >= 0)
        {
            if (needQuote)
            {
                destination[dstIndex--] = quote;
                needQuote = false;

                if (--quoteCount == 0)
                    goto CopyTo;
            }
            else if (source[srcIndex].Equals(quote))
            {
                destination[dstIndex--] = quote;
                srcIndex--;
                needQuote = true;
            }
            else
            {
                destination[dstIndex--] = source[srcIndex--];
            }
        }

        // true if the first token in the source is a quote
        if (needQuote)
        {
            destination[dstIndex--] = quote;
            quoteCount--;
        }

        // We can swallow the added complexity as the CopyTo can be over 10x faster than
        // the loop for longer inputs that don't have any quotes near the start
        // CopyTo is also close to O(1) for quoteless data
        CopyTo:
        if (srcIndex > 0)
        {
            if (ovrIndex >= 0)
            {
                source.Slice(srcIndex, ovrIndex + 1).CopyTo(overflow);
                srcIndex -= ovrIndex + 1;
            }

            if (dstIndex > 1)
            {
                source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
                dstIndex = 0;
            }
        }

        destination[dstIndex] = quote;

        Debug.Assert(dstIndex == 0);
        Debug.Assert(quoteCount == 0);
    }

    /// <summary>
    /// Escapes <see cref="source"/> into <see cref="destination"/>. Source and destination can overlap.
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="quote">Quote token</param>
    /// <param name="quoteCount">Amount of quotes in the source</param>
    /// <typeparam name="T">Token type</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Escape<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(destination.Length >= source.Length + quoteCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // Work backwards as the source and destination buffers might overlap
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
