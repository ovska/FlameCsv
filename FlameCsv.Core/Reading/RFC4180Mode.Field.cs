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

        ReadOnlySpan<T> remaining = state.remaining.Span;
        T delimiter = state._context.dialect.Delimiter;
        T quote = state._context.dialect.Quote;

        // If not the first column, validate that the first character is a delimiter
        if (!state.isAtStart)
        {
            // Since column count may not be known in advance, we leave the delimiter at the head after each column
            // so we can differentiate between an empty last column and end of the data in general
            if (!remaining[0].Equals(delimiter))
                state.ThrowNoDelimiterAtHead();

            state.remaining = state.remaining.Slice(1);
            remaining = remaining.Slice(1);
        }
        else
        {
            state.isAtStart = false;
        }

        // keep track of how many quotes the current column has
        uint quotesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;

        // If the remaining row has no quotes seek the next comma directly
        int index = quotesRemaining == 0
            ? remaining.IndexOf(delimiter)
            : remaining.IndexOfAny(delimiter, quote);

        ReadOnlyMemory<T> field;

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(delimiter))
            {
                field = state.remaining.Slice(0, index);
                state.remaining = state.remaining.Slice(index); // note: leave the comma in
                return quotesConsumed > 0
                    ? Unescape(field, quote, quotesConsumed, ref state.buffer)
                    : field;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            int nextIndex = --quotesRemaining == 0
                ? remaining.Slice(index).IndexOf(delimiter)
                : quotesConsumed % 2 == 0 // uneven quotes, only need to find the next one
                    ? remaining.Slice(index).IndexOfAny(delimiter, quote)
                    : remaining.Slice(index).IndexOf(quote);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        field = state.remaining;
        state.remaining = default; // consume all data

        state.EnsureFullyConsumed(-1);

        return quotesConsumed != 0
            ? Unescape(field, quote, quotesConsumed, ref state.buffer)
            : field;
    }
}
