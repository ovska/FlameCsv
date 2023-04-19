using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

internal interface ICsvMode<T> where T : unmanaged, IEquatable<T>
{
    bool TryGetLine(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount,
        bool isFinalBlock);

    bool TryGetField(
        ref CsvEnumerationStateRef<T> state,
        out ReadOnlyMemory<T> field);
}

internal static class RFC4180Mode<T> /*: ICsvMode<T>*/ where T : unmanaged, IEquatable<T>
{
    public static bool TryGetLine(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount,
        bool isFinalBlock)
    {
        return LineReader.TryGetLine(in dialect, ref sequence, out line, out quoteCount, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryGetField(
        ref CsvEnumerationStateRef<T> state,
        out ReadOnlyMemory<T> field)
    {
        if (state.remaining.IsEmpty)
        {
            if (state.QuotesRemaining != 0)
                ThrowInvalidQuoteCount(ref state);

            field = default;
            return false;
        }

        ReadOnlySpan<T> remaining = state.remaining.Span;
        CsvDialect<T> dialect = state.Dialect;

        // If not the first column, validate that the first character is a delimiter
        if (!state.isAtStart)
        {
            // Since column count may not be known in advance, we leave the delimiter at the head after each column
            // so we can differentiate between an empty last column and end of the data in general
            if (!remaining[0].Equals(dialect.Delimiter))
                ThrowNoDelimiterAtHead(ref state);

            state.remaining = state.remaining.Slice(1);
            remaining = remaining.Slice(1);
        }

        // keep track of how many quotes the current column has
        int quotesConsumed = 0;
        ref int quotesRemaining = ref state.QuotesRemaining;

        // If the remaining row has no quotes seek the next comma directly
        int index = state.QuotesRemaining == 0
            ? remaining.IndexOf(state.Dialect.Delimiter)
            : remaining.IndexOfAny(state.Dialect.Delimiter, state.Dialect.Quote);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(state.Dialect.Delimiter))
            {
                field = state.remaining.Slice(0, index).Unescape(dialect.Quote, quotesConsumed, ref state.unescapeBuffer);
                state.remaining = state.remaining.Slice(index); // note: leave the comma in
                state.isAtStart = false;
                return true;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            int nextIndex = --quotesRemaining == 0
                ? remaining.Slice(index).IndexOf(dialect.Delimiter)
                : quotesConsumed % 2 == 0 // uneven quotes, only need to find the next one
                    ? remaining.Slice(index).IndexOfAny(dialect.Delimiter, dialect.Quote)
                    : remaining.Slice(index).IndexOf(dialect.Quote);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        if (quotesRemaining != 0)
        {
            // there were leftover unprocessed quotes
            ThrowInvalidQuoteCount(ref state);
        }

        field = state.remaining.Unescape(dialect.Quote, quotesConsumed, ref state.unescapeBuffer);
        state.remaining = default; // consume all data
        state.isAtStart = false;
        return true;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNoDelimiterAtHead(ref CsvEnumerationStateRef<T> state)
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after first column), " +
            $"Remaining: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}, " +
            $"Record: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidQuoteCount(ref CsvEnumerationStateRef<T> state)
    {
        throw new UnreachableException(
            $"CSV record was fully consumed, but there were {state.QuotesRemaining} quotes left, " +
            $"Record: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }
}
