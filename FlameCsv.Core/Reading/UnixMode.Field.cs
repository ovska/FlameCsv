using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading;

internal static partial class UnixMode<T> where T : unmanaged, IBinaryInteger<T>
{
    public static ReadOnlySpan<T> ReadNextField(scoped ref CsvFieldReader<T> state)
    {
        Debug.Assert(!state.End, "ReadNextField called with empty input");
        Debug.Assert(state.Escape.HasValue);

        ReadOnlySpan<T> field;
        T quote = state.Quote;
        T escape = state.Escape.GetValueOrDefault();
        T delimiter = state.Delimiter;
        nuint consumed = 0;
        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;
        ref uint escapesRemaining = ref state.escapesRemaining;

        // TODO
        ref T first = ref state.GetRemainingRef(out _);
        nuint len = (uint)(state.Record.Length - state.Consumed);

        int sliceStart;

        if (!state.isAtStart)
        {
            if (!first.Equals(delimiter))
            {
                state.ThrowNoDelimiterAtHead();
            }

            // delimiter is left at the start of data after the first field has been read
            consumed++;
            sliceStart = 1;
        }
        else
        {
            sliceStart = 0;
        }

        T token;

        if (quotesRemaining == 0)
        {
            if (escapesRemaining == 0)
                goto NoQuotesNoEscapes;

            goto NoQuotesHasEscapes;
        }

        if (escapesRemaining == 0)
            goto HasQuotesNoEscapes;
        goto HasQuotesAndEscapes;

    NoQuotesNoEscapes:
        while (consumed + 8 < len)
        {
            if (delimiter.Equals(Unsafe.Add(ref first, consumed)))
                goto Done1;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto Done2;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto Done3;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto Done4;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 4)))
                goto Done5;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 5)))
                goto Done6;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 6)))
                goto Done7;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 7)))
                goto Done8;

            consumed += 8;
        }

        while (consumed + 4 < len)
        {
            if (delimiter.Equals(Unsafe.Add(ref first, consumed)))
                goto Done1;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto Done2;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto Done3;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto Done4;

            consumed += 4;
        }

        while (consumed < len)
        {
            if (Unsafe.Add(ref first, consumed++).Equals(delimiter))
            {
                goto Done;
            }
        }

        goto EOL;

    Done1:
        consumed += 1;
        goto Done;
    Done2:
        consumed += 2;
        goto Done;
    Done3:
        consumed += 3;
        goto Done;
    Done4:
        consumed += 4;
        goto Done;
    Done5:
        consumed += 5;
        goto Done;
    Done6:
        consumed += 6;
        goto Done;
    Done7:
        consumed += 7;
        goto Done;
    Done8:
        consumed += 8;
        goto Done;

    NoQuotesHasEscapes:
        while (consumed < len)
        {
            token = Unsafe.Add(ref first, consumed++);

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(escape))
            {
                if (consumed++ >= len)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto NoQuotesNoEscapes;

                goto NoQuotesHasEscapes;
            }
        }

        goto EOL;

    HasQuotesNoEscapes:
        while (consumed < len)
        {
            token = Unsafe.Add(ref first, consumed++);

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;
                goto InStringNoEscapes;
            }
        }

        goto EOL;

    HasQuotesAndEscapes:
        while (consumed < len)
        {
            token = Unsafe.Add(ref first, consumed++);

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;
                goto InStringWithEscapes;
            }
            else if (token.Equals(escape))
            {
                if (consumed++ >= len)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto HasQuotesNoEscapes;

                goto HasQuotesAndEscapes;
            }
        }

        goto EOL;

    InStringNoEscapes:
        while (consumed < len)
        {
            if (Unsafe.Add(ref first, consumed++).Equals(quote))
            {
                quotesConsumed++;

                if (--quotesRemaining == 0)
                    goto NoQuotesNoEscapes;

                goto HasQuotesNoEscapes;
            }
        }

        goto EOL;

    InStringWithEscapes:
        while (consumed < len)
        {
            token = Unsafe.Add(ref first, consumed++);

            if (token.Equals(quote))
            {
                quotesConsumed++;

                if (--quotesRemaining == 0)
                    goto NoQuotesHasEscapes;

                goto HasQuotesAndEscapes;
            }
            else if (token.Equals(escape))
            {
                if (consumed++ >= len)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto InStringNoEscapes;

                goto InStringWithEscapes;
            }
        }

    EOL:
        if ((quotesRemaining | escapesRemaining) != 0)
            state.ThrowForInvalidEOF();

        // consume the remaining
        field = state.Remaining.Slice(sliceStart);
        state.Consumed = state.Record.Length;
        goto Return;

    Done:
        int consumedi = (int)consumed - 1;
        field = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, sliceStart), consumedi - sliceStart);
        state.Consumed += consumedi;

    Return:
        state.isAtStart = false;

        if (quotesConsumed != 0)
        {
            first = ref Unsafe.Add(ref first, sliceStart);

            if (field.Length >= 2 && quote.Equals(first) && quote.Equals(Unsafe.Add(ref first, field.Length - 1)))
            {
                field = field[1..^1];
            }
            else
            {
                ThrowInvalidUnescape(field, quote, escape, quotesConsumed, escapesConsumed);
            }
        }

        if (escapesConsumed > 0)
        {
            Debug.Assert(quotesConsumed is 0 or 2, $"Line had escapes, expected 0 or 2 quotes, had {quotesConsumed}");
            int unescapedLength = GetUnescapedLength(field.Length, escapesConsumed);
            Span<T> unescapeBuffer = state.GetUnescapeBuffer(unescapedLength);
            Unescape(field, escape, escapesConsumed, unescapeBuffer);
            field = unescapeBuffer;
        }

        return state.Whitespace.IsEmpty
            ? field
            : field.Trim(state.Whitespace);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetUnescapedLength(int fieldLength, uint escapesRemaining)
    {
        return fieldLength - (int)escapesRemaining;
    }
}
