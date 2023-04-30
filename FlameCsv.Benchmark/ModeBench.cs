using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
public class ModeBench
{
    private static readonly CsvReadingContext<byte> _context = new(CsvUtf8ReaderOptions.Default, default);
    private static readonly (ReadOnlyMemory<byte> data, RecordMeta meta)[] _bytes
        = File.ReadAllLines(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.UTF8)
        .Select(Encoding.UTF8.GetBytes)
        .Select(b => (new ReadOnlyMemory<byte>(b), _context.GetRecordMeta(b)))
        .ToArray();

    [Benchmark(Baseline = true)]
    public void Old()
    {
        byte[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFCOLD<byte>(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        byte[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<byte> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFCNEW<byte>(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlyMemory<T> RFCNEW<T>(ref CsvEnumerationStateRef<T> state) where T : unmanaged, IEquatable<T>
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
            T token = span.DangerousGetReferenceAt(consumed++);

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

        goto EOL;

        ContinueReadInsideQuotes:
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(quote))
            {
                quotesConsumed++;
                quotesRemaining--;

                if (quotesRemaining == 0)
                    goto ContinueReadNoQuotes;

                goto ContinueReadUnknown;
            }
        }

        goto EOL;

        ContinueReadNoQuotes:
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(delimiter))
            {
                goto Done;
            }
        }

        EOL:
        if (quotesRemaining != 0)
            state.ThrowFieldEndedPrematurely();

        // whole line was consumed, skip the delimiter if it wasn't the first field
        field = state.remaining.Slice((!state.isAtStart).ToByte());
        state.remaining = default;
        goto Return;

        Done:
        int sliceStart = (!state.isAtStart).ToByte();
        int length = consumed - sliceStart - 1;
        field = state.remaining.Slice(sliceStart, length);
        state.remaining = state.remaining.Slice(consumed - 1);

        Return:
        state.isAtStart = false;
        return quotesConsumed == 0 ? field : RFC4180Mode<T>.Unescape(field, quote, quotesConsumed, ref state.buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlyMemory<T> RFCOLD<T>(ref CsvEnumerationStateRef<T> state) where T : unmanaged, IEquatable<T>
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
                return RFC4180Mode<T>.UnescapeRare(field, quote, quotesConsumed - 2, ref state.buffer);
            }
        }
        else
        {
            RFC4180Mode<T>.ThrowInvalidUnescape(span, quote, quotesConsumed);
        }

        return field;
    }
}
