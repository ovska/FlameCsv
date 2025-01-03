using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv.Reading;

static partial class UnixMode<T>
{
    public static void Unescape(
        ReadOnlySpan<T> source,
        T escape,
        uint escapeCount,
        scoped Span<T> destination)
    {
        Debug.Assert(escapeCount > 0);

        if (source[^1].Equals(escape))
            goto Invalid;

        int written = 0;
        int index = 0;
        uint escapesLeft = escapeCount;

        while (index < source.Length)
        {
            int next = source.Slice(index).IndexOf(escape);

            if (next < 0)
                break;

            if (next - index == source.Length)
                goto Invalid;

            source.Slice(index, next).CopyTo(destination.Slice(written));
            written += next;
            destination[written++] = source[++next + index];
            index += next + 1; // advance past the escaped value

            // Found all quotes, copy remaining data
            if (--escapesLeft == 0)
            {
                source.Slice(index).CopyTo(destination.Slice(written));
                // written += source.Length - index;
                return;
            }
        }

    Invalid:
        ThrowInvalidUnescape(source, null, escape, 0, escapeCount);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidUnescape(
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
            int actualQuoteCount = source.Count(quote.Value);
            if (actualQuoteCount != quoteCount)
            {
                error.Append($"String delimiter count {quoteCount} was invalid (actual was {actualQuoteCount}). ");
            }
        }

        int actualEscapeCount = source.Count(escape);
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
                quote.HasValue && token.Equals(quote)
                    ? '"'
                    : token.Equals(escape)
                        ? 'E'
                        : 'x');
        }

        error.Append(']');

        throw new UnreachableException(
            $"Internal error, failed to unescape (token: {typeof(T).FullName}): {error}"
#if DEBUG
           ,
            innerException: new CsvFormatException(source.ToString())
#endif
        );
    }
}
