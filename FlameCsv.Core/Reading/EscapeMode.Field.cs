﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal static partial class EscapeMode<T> where T : unmanaged, IEquatable<T>
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);
        Debug.Assert(state.Dialect.Escape.HasValue);

        ReadOnlySpan<T> remaining = state.remaining.Span;
        T delimiter = state.Dialect.Delimiter;
        T quote = state.Dialect.Quote;
        T escape = state.Dialect.Escape.Value;

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
        else
        {
            state.isAtStart = false;
        }

        // keep track of how many quotes the current column has
        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;
        ref uint escapesRemaining = ref state.escapesRemaining;

        int index = (quotesRemaining, escapesRemaining) switch
        {
            (0, 0) => remaining.IndexOf(delimiter),
            (0, _) => remaining.IndexOfAny(delimiter, escape),
            (_, 0) => remaining.IndexOfAny(delimiter, quote),
            (_, _) => remaining.IndexOfAny(delimiter, escape, quote)
        };

        ReadOnlyMemory<T> field;

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(delimiter))
            {
                field = state.remaining.Slice(0, index);
                state.remaining = state.remaining.Slice(index); // note: leave the comma in
                goto UnescapeAndReturn;
            }
            else if (remaining[index].Equals(escape))
            {
                escapesRemaining--;
                escapesConsumed++;

                if (++index >= remaining.Length)
                    ThrowEscapeAtEnd(ref state);

                // Move past the next character, break if it was the last one
                if (++index >= remaining.Length)
                    break;
            }
            else
            {
                // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
                quotesConsumed++;
                quotesRemaining--;
                index++; // move index past the quote
            }

            ReadOnlySpan<T> notYetRead = remaining.Slice(index);

            int nextIndex = (quotesRemaining, escapesRemaining) switch
            {
                (0, 0) => notYetRead.IndexOf(delimiter),
                (0, _) => notYetRead.IndexOfAny(delimiter, escape),
                (_, 0) => quotesConsumed % 2 != 0 ? notYetRead.IndexOf(quote) : notYetRead.IndexOfAny(quote, escape),
                (_, _) => quotesConsumed % 2 != 0 ? notYetRead.IndexOfAny(quote, escape) : notYetRead.IndexOfAny(quote, escape, delimiter),
            };

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        field = state.remaining;
        state.remaining = default; // consume all data

        state.EnsureFullyConsumed(-1);

        UnescapeAndReturn:
        return (quotesConsumed | escapesConsumed) != 0
            ? Unescape(field, quote, escape, quotesConsumed, escapesConsumed, ref state.buffer)
            : field;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEscapeAtEnd(ref CsvEnumerationStateRef<T> state)
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (escape token was the final character), " +
            $"Remaining: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}, " +
            $"Record: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNoDelimiterAtHead(ref CsvEnumerationStateRef<T> state)
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after first column), " +
            $"Remaining: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}, " +
            $"Record: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }
}
