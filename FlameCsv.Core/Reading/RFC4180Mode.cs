﻿using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

internal static partial class RFC4180Mode<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetField(
        ref CsvEnumerationStateRef<T> state,
        out ReadOnlyMemory<T> field)
    {
        if (!state.remaining.IsEmpty)
        {
            return TryGetFieldCore(ref state, out field);
        }

        if (state.quotesRemaining != 0)
            ThrowInvalidQuoteCount(ref state);

        field = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetFieldCore(
        ref CsvEnumerationStateRef<T> state,
        out ReadOnlyMemory<T> field)
    {
        Debug.Assert(!state.remaining.IsEmpty);

        ReadOnlySpan<T> remaining = state.remaining.Span;
        T delimiter = state.Dialect.Delimiter;
        T quote = state.Dialect.Quote;

        // If not the first column, validate that the first character is a delimiter
        if (!state.isAtStart)
        {
            // Since column count may not be known in advance, we leave the delimiter at the head after each column
            // so we can differentiate between an empty last column and end of the data in general
            if (!remaining[0].Equals(delimiter))
                ThrowNoDelimiterAtHead(ref state);

            state.remaining = state.remaining.Slice(1);
            remaining = remaining.Slice(1);
        }

        // keep track of how many quotes the current column has
        int quotesConsumed = 0;
        ref int quotesRemaining = ref state.quotesRemaining;

        // If the remaining row has no quotes seek the next comma directly
        int index = quotesRemaining == 0
            ? remaining.IndexOf(delimiter)
            : remaining.IndexOfAny(delimiter, quote);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(delimiter))
            {
                field = state.remaining.Slice(0, index).Unescape(quote, quotesConsumed, ref state.unescapeBuffer);
                state.remaining = state.remaining.Slice(index); // note: leave the comma in
                state.isAtStart = false;
                return true;
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

        if (quotesRemaining != 0)
        {
            // there were leftover unprocessed quotes
            ThrowInvalidQuoteCount(ref state);
        }

        field = state.remaining.Unescape(quote, quotesConsumed, ref state.unescapeBuffer);
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
            $"CSV record was fully consumed, but there were {state.quotesRemaining} quotes left, " +
            $"Record: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }
}