using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// <param name="array">Rented buffer used if the data has quotes in-between the wrapping quotes</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>Unescaped tokens, might be a slice of the original input</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Unescape<T>(
        this ReadOnlySpan<T> source,
        T quote,
        int quoteCount,
        ref T[]? array)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);

        if (source.Length >= 2 && source[0].Equals(quote) && source[^1].Equals(quote))
        {
            // Trim trailing and leading quotes
            source = source.Slice(1, source.Length - 2);

            if (quoteCount == 2)
            {
                return source;
            }

            return source.UnescapeRare(quote, quoteCount - 2, ref array);
        }

        return ThrowInvalidUnescape(source, quote, quoteCount);
    }

    /// <summary>
    /// Unescapes inner quotes from the input. Wrapping quotes have been trimmed at this point.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining common case above
    private static ReadOnlySpan<T> UnescapeRare<T>(
        this ReadOnlySpan<T> source,
        T quote,
        int quoteCount,
        ref T[]? array)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);

        int written = 0;
        int index = 0;
        int quotesLeft = quoteCount;
        ReadOnlySpan<T> needle = stackalloc T[] { quote, quote };

        int requiredLength = source.Length - quoteCount / 2;
        ArrayPool<T>.Shared.EnsureCapacity(ref array, requiredLength);
        Span<T> buffer = array;

        while (index < source.Length)
        {
            int next = source.Slice(index).IndexOf(needle);

            if (next < 0)
                break;

            int toCopy = next + 1;
            source.Slice(index, toCopy).CopyTo(buffer.Slice(written));
            written += toCopy;
            index += toCopy + 1; // advance past the second quote

            // Found all quotes, copy remaining data
            if ((quotesLeft -= 2) == 0)
            {
                source.Slice(index).CopyTo(buffer.Slice(written));
                written += source.Length - index;
                return buffer.Slice(0, written);
            }
        }

        return ThrowInvalidUnescape(source, quote, quoteCount);
    }

    /// <inheritdoc cref="Unescape{T}(System.ReadOnlySpan{T},T,int,ref T[])"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> Unescape<T>(
        this ReadOnlyMemory<T> source,
        T quote,
        int quoteCount,
        ref T[]? array)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);

        var span = source.Span;

        if (span.Length >= 2)
        {
            if (span[0].Equals(quote) && span[^1].Equals(quote))
            {
                // Trim trailing and leading quotes
                source = source.Slice(1, source.Length - 2);

                if (quoteCount == 2)
                {
                    return source;
                }

                return source.UnescapeRare(quote, quoteCount - 2, ref array);
            }
        }

        return ThrowInvalidUnescape(source.Span, quote, quoteCount);
    }

    /// <inheritdoc cref="UnescapeRare{T}(System.ReadOnlySpan{T},T,int,ref T[])"/>
    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining common case above
    private static ReadOnlyMemory<T> UnescapeRare<T>(
        this ReadOnlyMemory<T> source,
        T quote,
        int quoteCount,
        ref T[]? array)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(quoteCount >= 2);
        Debug.Assert(quoteCount % 2 == 0);

        int written = 0;
        int index = 0;
        int quotesLeft = quoteCount;

        var sourceSpan = source.Span;
        ReadOnlySpan<T> needle = stackalloc T[] { quote, quote };

        int requiredLength = source.Length - quoteCount / 2;
        ArrayPool<T>.Shared.EnsureCapacity(ref array, requiredLength);
        Memory<T> buffer = array;

        while (index < source.Length)
        {
            int next = sourceSpan.Slice(index).IndexOf(needle);

            if (next < 0)
                break;

            int toCopy = next + 1;
            source.Slice(index, toCopy).CopyTo(buffer.Slice(written));
            written += toCopy;
            index += toCopy + 1; // advance past the second quote

            // Found all quotes, copy remaining data
            if ((quotesLeft -= 2) == 0)
            {
                source.Slice(index).CopyTo(buffer.Slice(written));
                written += source.Length - index;
                return buffer.Slice(0, written);
            }
        }

        return ThrowInvalidUnescape(source.Span, quote, quoteCount);
    }

    /// <exception cref="InvalidOperationException">
    /// The data and/or the supplied quote count parameter were invalid. 
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static T[] ThrowInvalidUnescape<T>(
        ReadOnlySpan<T> source,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        int actualCount = source.Count(quote);

        var error = new StringBuilder(64);

        if (source.Length < 2)
        {
            error.Append($"Source is too short (length: {source.Length}). ");
        }

        if (actualCount != quoteCount)
        {
            error.Append($"String delimiter count {quoteCount} was invalid (actual was {actualCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in source)
        {
            error.Append(token.Equals(quote) ? '"' : 'x');
        }

        error.Append(']');

        throw new InvalidOperationException(
            $"Internal error: failed to unescape {typeof(ReadOnlySpan<T>).ToTypeString()}: {error}");
    }
}
