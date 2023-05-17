using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

internal static partial class EscapeMode<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);
        Debug.Assert(state._context.Dialect.Escape.HasValue);

        ReadOnlyMemory<T> field;
        T quote = state._context.Dialect.Quote;
        T escape = state._context.Dialect.Escape.Value;
        T delimiter = state._context.Dialect.Delimiter;
        nuint consumed = 0;
        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;
        ref uint escapesRemaining = ref state.escapesRemaining;

        ref T first = ref MemoryMarshal.GetReference(state.remaining.Span);
        nuint len = (uint)state.remaining.Length;

        if (!state.isAtStart)
        {
            if (!first.Equals(delimiter))
            {
                state.ThrowNoDelimiterAtHead();
            }

            consumed++;
        }

        T token;

        if (quotesRemaining == 0)
        {
            if (escapesRemaining == 0)
                goto NoQuotesNoEscapes;

            goto NoQuotesHasEscapes;
        }
        else
        {
            if (quotesConsumed % 2 == 0)
            {
                if (escapesRemaining == 0)
                    goto HasQuotesNoEscapes;
                goto HasQuotesAndEscapes;
            }
            else
            {
                if (escapesRemaining == 0)
                    goto InStringNoEscapes;
                goto InStringWithEscapes;
            }
        }

        NoQuotesNoEscapes:
        while (consumed < len)
        {
            if (Unsafe.Add(ref first, consumed++).Equals(delimiter))
            {
                goto Done;
            }
        }

        goto EOL;

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

        // whole line was consumed, skip the delimiter if it wasn't the first field
        field = state.remaining.Slice((!state.isAtStart).ToByte());
        state.remaining = default;
        goto Return;

        Done:
        int sliceStart = (!state.isAtStart).ToByte();
        int length = (int)consumed - sliceStart - 1;
        field = state.remaining.Slice(sliceStart, length);
        state.remaining = state.remaining.Slice((int)consumed - 1);

        Return:
        state.isAtStart = false;

        if ((quotesConsumed | escapesConsumed) != 0)
            field = EscapeMode<T>.Unescape(field, quote, escape, quotesConsumed, escapesConsumed, ref state.buffer);

        return state._context.Dialect.Whitespace.IsEmpty
            ? field
            : field.Trim(state._context.Dialect.Whitespace.Span);
    }
}

