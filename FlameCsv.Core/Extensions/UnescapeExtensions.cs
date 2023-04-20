using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Extensions;

internal static class UnescapeExtensions
{
    /// <summary>
    /// Unescapes wrapping and inner double quotes from the input.
    /// The input must be wrapped in quotes, and other quotes in the input must be in pairs
    /// </summary>
    /// <remarks>
    /// Examples:
    /// [<c>"abc"</c>] unescapes into [<c>abc</c>], [<c>"A ""B"" C"</c>] unescapes into [<c>A "B" C</c>]
    /// </remarks>
    /// <param name="source">Data to unescape</param>
    /// <param name="quote">Double quote token</param>
    /// <param name="quoteCount">Known quote count in the data, must be over 0 and divisible by 2</param>
    /// <param name="unescapeBuffer">Buffer used if the data has quotes in-between the wrapping quotes</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>Unescaped tokens, might be a slice of the original input</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> Unescape<T>(
        this ReadOnlyMemory<T> source,
        T quote,
        int quoteCount,
        ref Memory<T> unescapeBuffer)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount != 0);
        Debug.Assert(quoteCount % 2 == 0);

        ReadOnlySpan<T> span = source.Span;

        if (span.Length >= 2 &&
            span.DangerousGetReference().Equals(quote) &&
            span.DangerousGetReferenceAt(span.Length - 1).Equals(quote))
        {
            // Trim trailing and leading quotes
            source = source.Slice(1, source.Length - 2);

            if (quoteCount != 2)
            {
                return UnescapeRare(source, quote, quoteCount - 2, ref unescapeBuffer);
            }
        }
        else
        {
            ThrowInvalidUnescape(span, quote, quoteCount);
        }

        return source;
    }

    /// <summary>
    /// Unescapes inner quotes from the input. Wrapping quotes have been trimmed at this point.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining common case above
    private static ReadOnlyMemory<T> UnescapeRare<T>(
        ReadOnlyMemory<T> sourceMemory,
        T quote,
        int quoteCount,
        ref Memory<T> unescapeBuffer)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);
        Debug.Assert(!unescapeBuffer.Span.Overlaps(sourceMemory.Span), "Source and unescape buffer must not overlap");

        int written = 0;
        int index = 0;
        int quotesLeft = quoteCount;

        var source = sourceMemory.Span;
        ReadOnlySpan<T> needle = stackalloc T[] { quote, quote };

        int unescapedLength = sourceMemory.Length - quoteCount / 2;

        if (unescapedLength > unescapeBuffer.Length)
            ThrowUnescapeBufferTooSmall(unescapedLength, unescapeBuffer.Length);

        Memory<T> buffer = unescapeBuffer.Slice(0, unescapedLength);
        unescapeBuffer = unescapeBuffer.Slice(unescapedLength); // "consume" the buffer

        while (index < sourceMemory.Length)
        {
            int next = source.Slice(index).IndexOf(needle);

            if (next < 0)
                break;

            int toCopy = next + 1;
            sourceMemory.Slice(index, toCopy).CopyTo(buffer.Slice(written));
            written += toCopy;
            index += toCopy + 1; // advance past the second quote

            // Found all quotes, copy remaining data
            if ((quotesLeft -= 2) == 0)
            {
                sourceMemory.Slice(index).CopyTo(buffer.Slice(written));
                written += sourceMemory.Length - index;
                return buffer.Slice(0, written);
            }
        }

        ThrowInvalidUnescape(sourceMemory.Span, quote, quoteCount);
        return default; // unreachable
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnescapeBufferTooSmall(int requiredLength, int bufferLength)
    {
        throw new UnreachableException(
            $"Internal error, failed to unescape: required {requiredLength} but got buffer with length {bufferLength}.");
    }

    /// <exception cref="UnreachableException">
    /// The data and/or the supplied quote count parameter were invalid. 
    /// </exception>
    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidUnescape<T>(
        ReadOnlySpan<T> source,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        int actualCount = source.Count(quote);

        var error = new StringBuilder(64);

        if (source.Length < 2)
        {
            error.Append(CultureInfo.InvariantCulture, $"Source is too short (length: {source.Length}). ");
        }

        if (actualCount != quoteCount)
        {
            error.Append(CultureInfo.InvariantCulture, $"String delimiter count {quoteCount} was invalid (actual was {actualCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in source)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new UnreachableException(
            $"Internal error, failed to unescape (token: {typeof(T).ToTypeString()}): {error}");
    }
}
