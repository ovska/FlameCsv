using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

internal static partial class RFC4180Mode<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Reads the next field from the state if it is not empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetField(
        ref CsvEnumerationStateRef<T> state,
        out ReadOnlyMemory<T> field)
    {
        if (!state.remaining.IsEmpty)
        {
            field = ReadNextField(ref state);
            return true;
        }

        state.EnsureFullyConsumed(-1);
        field = default;
        return false;
    }

    /// <summary>
    /// Reads the next field from a <strong>non-empty</strong> state.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);

        ReadOnlySpan<T> span = state.remaining.Span;
        T delimiter = state._context.Dialect.Delimiter;
        T quote = state._context.Dialect.Quote;

        // If not the first field, validate that the first character is a delimiter
        if (!state.isAtStart)
        {
            // Since field count may not be known in advance, we leave the delimiter at the head after each field
            // so we can differentiate between an empty last field and end of the data in general
            if (!span[0].Equals(delimiter))
                state.ThrowNoDelimiterAtHead();

            state.remaining = state.remaining.Slice(1);
            span = span.Slice(1);
        }
        else
        {
            state.isAtStart = false;
        }

        // keep track of how many quotes the current field has
        uint quotesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;

        // If the remaining row has no quotes seek the next comma directly
        int index = quotesRemaining == 0
            ? span.IndexOf(delimiter)
            : span.IndexOfAny(delimiter, quote);

        ReadOnlyMemory<T> field;

        while (index >= 0)
        {
            // Hit a comma, either found end of field or more fields than expected
            if (span[index].Equals(delimiter))
            {
                field = state.remaining.Slice(0, index);
                span = span.Slice(0, index);
                state.remaining = state.remaining.Slice(index); // note: leave the comma in
                goto UnescapeAndReturn;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            int nextIndex = --quotesRemaining == 0
                ? span.Slice(index).IndexOf(delimiter)
                : quotesConsumed % 2 == 0 // uneven quotes, only need to find the next one
                    ? span.Slice(index).IndexOfAny(delimiter, quote)
                    : span.Slice(index).IndexOf(quote);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        field = state.remaining;
        state.remaining = default; // consume all data

        state.EnsureFullyConsumed(-1);

        UnescapeAndReturn:
        if (quotesConsumed == 0)
            return field; // no unescaping needed

        if (span.Length >= 2 &&
            span.DangerousGetReference().Equals(quote) &&
            span.DangerousGetReferenceAt(span.Length - 1).Equals(quote))
        {
            // Trim trailing and leading quotes
            field = field.Slice(1, field.Length - 2);

            if (quotesConsumed != 2)
            {
                Debug.Assert(quotesConsumed >= 4);
                return UnescapeRare(field, quote, quotesConsumed - 2, ref state.buffer);
            }
        }
        else
        {
            ThrowInvalidUnescape(span, quote, quotesConsumed);
        }

        return field;
    }
}
