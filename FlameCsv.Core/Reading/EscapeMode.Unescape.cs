using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv.Reading;

internal static partial class EscapeMode<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // encourage inlining common case above
    public static ReadOnlyMemory<T> Unescape(
        ReadOnlyMemory<T> source,
        T quote,
        T escape,
        uint quoteCount,
        uint escapeCount,
        ref Memory<T> unescapeBuffer)
    {
        Debug.Assert(
            quoteCount == 0 ? escapeCount > 0 : quoteCount == 2,
            $"Invalid args, Quotes {quoteCount}, Escapes {escapeCount}");

        if (quoteCount != 0)
        {
            ReadOnlySpan<T> span = source.Span;

            if (quoteCount == 2 &&
                span.Length >= 2 &&
                span.DangerousGetReference().Equals(quote) &&
                span.DangerousGetReferenceAt(span.Length - 1).Equals(quote))
            {
                source = source.Slice(1, source.Length - 2);
            }
            else
            {
                return ThrowInvalidUnescape(span, quote, escape, quoteCount, escapeCount);
            }
        }

        return escapeCount == 0
            ? source
            : UnescapeRare(source, escape, escapeCount, ref unescapeBuffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining common case above
    internal static ReadOnlyMemory<T> UnescapeRare(
        ReadOnlyMemory<T> sourceMemory,
        T escape,
        uint escapeCount,
        ref Memory<T> unescapeBuffer)
    {
        Debug.Assert(escapeCount > 0);
        Debug.Assert(!unescapeBuffer.Span.Overlaps(sourceMemory.Span), "Source and unescape buffer must not overlap");

        ReadOnlySpan<T> source = sourceMemory.Span;

        if (source[^1].Equals(escape))
            goto Invalid;

        uint unescapedLength = (uint)sourceMemory.Length - escapeCount;

        if (unescapedLength > unescapeBuffer.Length)
            ThrowUnescapeBufferTooSmall((int)unescapedLength, unescapeBuffer.Length);

        Memory<T> destination = unescapeBuffer.Slice(0, (int)unescapedLength);
        Span<T> destinationSpan = unescapeBuffer.Span;
        unescapeBuffer = unescapeBuffer.Slice((int)unescapedLength); // "consume" the buffer

        int written = 0;
        int index = 0;
        uint escapesLeft = escapeCount;

        while (index < sourceMemory.Length)
        {
            int next = source.Slice(index).IndexOf(escape);

            if (next < 0)
                break;

            if (next - index == source.Length)
                goto Invalid;

            sourceMemory.Slice(index, next).CopyTo(destination.Slice(written));
            written += next;
            destinationSpan[written++] = source[++next + index];
            index += next + 1; // advance past the escaped value

            // Found all quotes, copy remaining data
            if (--escapesLeft == 0)
            {
                sourceMemory.Slice(index).CopyTo(destination.Slice(written));
                written += sourceMemory.Length - index;
                return destination.Slice(0, written);
            }
        }

        Invalid:
        return ThrowInvalidUnescape(sourceMemory.Span, null, escape, 0, escapeCount);
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnescapeBufferTooSmall(int requiredLength, int bufferLength)
    {
        throw new UnreachableException(
            $"Internal error, failed to unescape: required {requiredLength} but got buffer with length {bufferLength}.");
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlyMemory<T> ThrowInvalidUnescape(
        ReadOnlySpan<T> source,
        T? quote,
        T escape,
        uint quoteCount,
        uint escapeCount)
    {
        var error = new StringBuilder(64);

        if (source.Length < 2)
        {
            error.Append($"Source is too short (length: {source.Length}). ");
        }
        else if (source[^1].Equals(escape))
        {
            error.Append("The source ended with an escape character. ");
        }

        if (quote.HasValue)
        {
            int actualQuoteCount = System.MemoryExtensions.Count(source, quote.Value);
            if (actualQuoteCount != quoteCount)
            {
                error.Append($"String delimiter count {quoteCount} was invalid (actual was {actualQuoteCount}). ");
            }
        }

        int actualEscapeCount = System.MemoryExtensions.Count(source, escape);
        if (actualEscapeCount != escapeCount)
        {
            error.Append($"Escape character count {escapeCount} was invalid (actual was {actualEscapeCount}). ");
        }

        if (error.Length != 0)
            error.Length--;

        error.Append("The data structure was: [");

        foreach (var token in source)
        {
            error.Append(
                quote.HasValue && token.Equals(quote) ? '"' :
                token.Equals(escape) ? 'E'
                : 'x');
        }

        error.Append(']');

        throw new UnreachableException(
            $"Internal error, failed to unescape (token: {typeof(T).ToTypeString()}): {error}"
#if DEBUG
            , innerException: new CsvFormatException(source.ToString())
#endif
            );
    }
}
