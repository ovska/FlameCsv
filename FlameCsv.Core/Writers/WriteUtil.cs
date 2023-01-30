using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Formatters;

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
            Span<T> written = destination[..tokensWritten];

            if (NeedsEscaping(written, in tokens, out int quoteCount))
            {
                int escapedLength = tokensWritten + 2 + quoteCount;

                if (destination.Length < escapedLength)
                {
                    PartialEscape(
                        written,
                        destination,
                        tokens.StringDelimiter,
                        quoteCount,
                        ref array);
                    overflowWritten = escapedLength - destination.Length;
                    return true;
                }

                Escape(written, destination, tokens.StringDelimiter, quoteCount);
                tokensWritten = escapedLength;
                Debug.Assert(destination[0].Equals(tokens.StringDelimiter));
                Debug.Assert(destination[tokensWritten].Equals(tokens.StringDelimiter));
            }
        }

        overflowWritten = 0;
        return true;
    }

    /// <summary>
    /// Checks if the written value contains commas, quotes, or a newline.
    /// </summary>
    /// <param name="value">Non-empty value span</param>
    /// <param name="tokens">Tokens instance</param>
    /// <param name="quoteCount">Amount of quotes in the data if it needs escaping</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns><see langword="true"/> if the value needs to be escaped</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsEscaping<T>(
        ReadOnlySpan<T> value,
        in CsvTokens<T> tokens,
        out int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!value.IsEmpty);

        ReadOnlySpan<T> newline = tokens.NewLine.Span;

        // For 1 token newlines we can expedite the search
        int index = newline.Length == 1
            ? value.IndexOfAny(tokens.Delimiter, tokens.StringDelimiter, newline[0])
            : value.IndexOfAny(tokens.Delimiter, tokens.StringDelimiter);

        if (index >= 0)
        {
            // we know any token before index cannot be a quote
            quoteCount = value.Slice(index).Count(tokens.StringDelimiter);
            return true;
        }

        quoteCount = 0;

        // only possible escaping scenario left is a multitoken newline
        return newline.Length > 1 && value.IndexOf(newline) >= 0;
    }

    /// <summary>
    /// Escapes the data in <paramref name="source"/>, writing much data as possible to <paramref name="destination"/>
    /// with the leftovers being written to <paramref name="array"/>. Length of the data escaped to array is:
    /// <c>source.Length + quoteCount + 2 - destination.Length</c>
    /// </summary>
    /// <param name="source">Data that needs escaping</param>
    /// <param name="destination">Destination buffer, can be the same memory region as source</param>
    /// <param name="quote">Quote token</param>
    /// <param name="quoteCount">Amount of quotes in the source</param>
    /// <param name="array">
    /// Pooled buffer used to write the rest of the data, guaranteed to not be null after this method returns
    /// </param>
    /// <typeparam name="T">Token type</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)] // rare-ish, doesn't need to be inlined
    public static void PartialEscape<T>(
        ReadOnlySpan<T> source,
        Span<T> destination,
        T quote,
        int quoteCount,
        [NotNull] ref T[]? array)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        int requiredLength = source.Length + quoteCount + 2;

        // Ensure the buffer has enough space for the overflowing escaped data
        ArrayPool<T>.Shared.EnsureCapacity(ref array, requiredLength - destination.Length);

        // First write the overflowing data to the array, working backwards as source and destination
        // share a memory region
        int srcIndex = source.Length - 1;
        int ovrIndex = requiredLength - destination.Length - 1;
        int dstIndex = destination.Length - 1;
        bool needQuote = false;

        Span<T> overflow = array;
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
