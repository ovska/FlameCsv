using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
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

        ReadOnlyMemory<T> field;
        ReadOnlySpan<T> span = state.remaining.Span;
        T quote = state._context.Dialect.Quote;
        T delimiter = state._context.Dialect.Delimiter;
        int consumed = 0;
        uint quotesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;

        if (!state.isAtStart && !span[consumed++].Equals(delimiter))
        {
            state.ThrowNoDelimiterAtHead();
        }

        if (quotesRemaining == 0)
            goto ContinueReadNoQuotes;

        ContinueReadUnknown:
        while (consumed < span.Length)
        {
            T token = span[consumed++];

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;
                goto ContinueReadInsideQuotes;
            }
        }

        ContinueReadInsideQuotes:
        while (consumed < span.Length)
        {
            if (span[consumed++].Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;

                if (quotesRemaining == 0)
                    goto ContinueReadNoQuotes;

                goto ContinueReadUnknown;
            }
        }

        ContinueReadNoQuotes:
        while (consumed < span.Length)
        {
            if (span[consumed++].Equals(delimiter))
            {
                goto Done;
            }
        }

        if (quotesRemaining != 0)
            state.ThrowFieldEndedPrematurely();

        // whole line was consumed, skip the delimiter if it wasn't the first field
        field = state.remaining.Slice((!state.isAtStart).ToByte());
        state.remaining = default;
        goto Return;

        Done:
        int sliceStart = state.isAtStart ? 0 : 1;
        int length = consumed - sliceStart - 1;
        field = state.remaining.Slice(sliceStart, length);
        state.remaining = state.remaining.Slice(consumed - 1);

        Return:
        state.isAtStart = false;
        return quotesConsumed == 0 ? field : RFC4180Mode<T>.Unescape(field, quote, quotesConsumed, ref state.buffer);
    }
}
