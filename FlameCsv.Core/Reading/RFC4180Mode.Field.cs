using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

internal static partial class RFC4180Mode<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Reads the next field from a <strong>non-empty</strong> state.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);

        ReadOnlyMemory<T> field;
        T quote = state._context.Dialect.Quote;
        T delimiter = state._context.Dialect.Delimiter;
        nuint consumed = 0;
        uint quotesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;

        ref T first = ref MemoryMarshal.GetReference(state.remaining.Span);
        nuint remaining = (uint)state.remaining.Length;
        T lookUp;

        int sliceStart = (!state.isAtStart).ToByte();

        if (!state.isAtStart)
        {
            if (!first.Equals(delimiter))
            {
                state.ThrowNoDelimiterAtHead();
            }

            // delimiter is left at the start of data after the first field has been read
            consumed++;
            remaining--;
        }

        if (quotesRemaining == 0 || !quote.Equals(Unsafe.Add(ref first, consumed)))
        {
            goto ContinueReadNoQuotes;
        }

        StringStarted:
        consumed++;
        remaining--;
        quotesConsumed++;
        quotesRemaining--;

        while (remaining >= 8)
        {
            if (quote.Equals(Unsafe.Add(ref first, consumed + 0)))
                goto StringEnded;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto StringEnded1;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto StringEnded2;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto StringEnded3;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 4)))
                goto StringEnded4;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 5)))
                goto StringEnded5;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 6)))
                goto StringEnded6;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 7)))
                goto StringEnded7;

            consumed += 8;
            remaining -= 8;
        }

        while (remaining >= 4)
        {
            if (quote.Equals(Unsafe.Add(ref first, consumed + 0)))
                goto StringEnded;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto StringEnded1;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto StringEnded2;

            if (quote.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto StringEnded3;

            consumed += 4;
            remaining -= 4;
        }

        while (remaining > 0)
        {
            if (quote.Equals(Unsafe.Add(ref first, consumed)))
                goto StringEnded;

            consumed++;
            remaining--;
        }

        goto EOL;

        StringEnded1:
        consumed += 1;
        remaining -= 1;
        goto StringEnded;
        StringEnded2:
        consumed += 2;
        remaining -= 2;
        goto StringEnded;
        StringEnded3:
        consumed += 3;
        remaining -= 3;
        goto StringEnded;
        StringEnded4:
        consumed += 4;
        remaining -= 4;
        goto StringEnded;
        StringEnded5:
        consumed += 5;
        remaining -= 5;
        goto StringEnded;
        StringEnded6:
        consumed += 6;
        remaining -= 6;
        goto StringEnded;
        StringEnded7:
        consumed += 7;
        remaining -= 7;
        goto StringEnded;

        StringEnded:
        consumed++;
        remaining--;
        quotesRemaining--;
        quotesConsumed++;

        if (remaining == 0)
        {
            goto EOL;
        }

        lookUp = Unsafe.Add(ref first, consumed);

        if (lookUp.Equals(quote))
        {
            goto StringStarted;
        }
        else if (lookUp.Equals(delimiter))
        {
            goto Done;
        }
        else
        {
            state.ThrowForInvalidEndOfString();
        }

        ContinueReadNoQuotes:
        while (remaining >= 8)
        {
            if (delimiter.Equals(Unsafe.Add(ref first, consumed)))
                goto Done;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto Done1;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto Done2;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto Done3;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 4)))
                goto Done4;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 5)))
                goto Done5;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 6)))
                goto Done6;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 7)))
                goto Done7;

            consumed += 8;
            remaining -= 8;
        }

        while (remaining >= 4)
        {
            if (delimiter.Equals(Unsafe.Add(ref first, consumed)))
                goto Done;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 1)))
                goto Done1;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 2)))
                goto Done2;

            if (delimiter.Equals(Unsafe.Add(ref first, consumed + 3)))
                goto Done3;

            consumed += 4;
            remaining -= 4;
        }

        while (remaining > 0)
        {
            if (delimiter.Equals(Unsafe.Add(ref first, consumed)))
                goto Done;

            consumed++;
            remaining--;
        }

        goto EOL;

        Done1:
        consumed += 1;
        remaining -= 1;
        goto Done;
        Done2:
        consumed += 2;
        remaining -= 2;
        goto Done;
        Done3:
        consumed += 3;
        remaining -= 3;
        goto Done;
        Done4:
        consumed += 4;
        remaining -= 4;
        goto Done;
        Done5:
        consumed += 5;
        remaining -= 5;
        goto Done;
        Done6:
        consumed += 6;
        remaining -= 6;
        goto Done;
        Done7:
        consumed += 7;
        remaining -= 7;
        goto Done;

        EOL:
        if (quotesRemaining != 0)
            state.ThrowForInvalidEOF();

        field = state.remaining.Slice(sliceStart);
        state.remaining = default;
        goto Return;

        Done:
        field = state.remaining[sliceStart..(int)consumed];
        state.remaining = state.remaining.Slice((int)consumed);

        Return:
        state.isAtStart = false;

        if (quotesConsumed != 0)
        {
            first = ref Unsafe.Add(ref first, sliceStart);

            if (field.Length >= 2 && quote.Equals(first) && quote.Equals(Unsafe.Add(ref first, field.Length - 1)))
            {
                field = field[1..^1];

                if (quotesConsumed != 2)
                {
                    Debug.Assert(quotesConsumed >= 4);
                    field = Unescape(field, quote, quotesConsumed - 2, ref state.buffer);
                }
            }
            else
            {
                ThrowInvalidUnescape(field, quote, quotesConsumed);
            }
        }

        if (!state._context.Dialect.Whitespace.IsEmpty)
            field = field.Trim(state._context.Dialect.Whitespace.Span);

        return field;
    }
}
