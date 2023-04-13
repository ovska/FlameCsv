using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading;

namespace FlameCsv.Writers;

internal static class WriteUtil<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns whether the value contains a delimiter, quote or newline characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsQuoting(
        scoped ReadOnlySpan<T> value,
        in CsvDialect<T> dialect,
        out int quoteCount)
    {
        Debug.Assert(!value.IsEmpty);

        int index;

        if (dialect.Newline.Length == 1)
        {
            index = value.IndexOfAny(dialect.Delimiter, dialect.Quote, dialect.Newline.Span[0]);

            if (index >= 0)
            {
                goto Found;
            }
        }
        else
        {
            index = value.IndexOfAny(dialect.Delimiter, dialect.Quote);

            if (index >= 0)
            {
                goto Found;
            }

            quoteCount = 0;
            return value.IndexOf(dialect.Newline.Span) >= 0;
        }

        Unsafe.SkipInit(out quoteCount);
        return false;

        Found:
        quoteCount = value.Slice(index).Count(dialect.Quote);
        return true;
    }

    /// <summary>
    /// Escapes <paramref name="source"/> into <paramref name="destination"/> by wrapping it in quotes and escaping
    /// possible quotes in the value.
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="quote">Quote token</param>
    /// <param name="quoteCount">Amount of quotes in the source</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Escape(
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        T quote,
        int quoteCount)
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

    /// <summary>
    /// Escapes the data in <paramref name="source"/>, writing much data as possible to <paramref name="destination"/>
    /// with the leftovers being written to <paramref name="overflowBuffer"/>.
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="quote">Quote token</param>
    /// <param name="quoteCount">Amount of quotes in the source</param>
    /// <param name="overflowBuffer">Buffer to write the overlowing part to</param>
    /// <returns>A memory wrapping around the parts in the overflow buffer that were written to</returns>
    [MethodImpl(MethodImplOptions.NoInlining)] // rare-ish, doesn't need to be inlined
    public static ReadOnlyMemory<T> EscapeWithOverflow(
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        T quote,
        int quoteCount,
        scoped ValueBufferOwner<T> overflowBuffer)
    {
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        int requiredLength = source.Length + quoteCount + 2;

        // First write the overflowing data to the array, working backwards as source and destination
        // share a memory region
        int srcIndex = source.Length - 1;
        int ovrIndex = requiredLength - destination.Length - 1;
        int dstIndex = destination.Length - 1;
        bool needQuote = false;

        Memory<T> overflowMemory = overflowBuffer.GetMemory(requiredLength - destination.Length);
        Span<T> overflow = overflowMemory.Span;
        overflow[ovrIndex--] = quote; // write closing quote

        // Short circuit to faster impl if there are no quotes in the source data
        if (quoteCount == 0)
            goto CopyTo;

        // Copy tokens one-at-a-time until all quotes have been escaped and use the faster impl after
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

        CopyTo:
        if (srcIndex > 0)
        {
            if (ovrIndex >= 0)
            {
                source.Slice(srcIndex, ovrIndex + 1).CopyTo(overflow);
                srcIndex -= ovrIndex + 1;
            }

            // dst needs 1 slot for the opening quote
            if (dstIndex > 1)
            {
                source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
                dstIndex = 0;
            }
        }

        destination[dstIndex] = quote; // write opening quote

        Debug.Assert(dstIndex == 0);
        Debug.Assert(quoteCount == 0);

        return overflowMemory;
    }
}
