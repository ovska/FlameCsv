using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
public class ModeBench2
{
    private static readonly CsvReadingContext<char> _context = new(CsvTextReaderOptions.Default, new() { Escape = '\\', ExposeContent = true });
    private static readonly (ReadOnlyMemory<char> data, RecordMeta meta)[] _bytes
        = File.ReadAllLines(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.UTF8)
        .Select(b => b.Replace(",\"\"\"", ",\"\\\"").Replace("\"\"", "\\\"").AsMemory())
        .Select(b => ((ReadOnlyMemory<char>)b, _context.GetRecordMeta(b)))
        .ToArray();

    [Benchmark(Baseline = true)]
    public void Old()
    {
        char[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<char> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFCOLD<char>(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        char[]? array = null;

        foreach (ref readonly var tuple in _bytes.AsSpan())
        {
            CsvEnumerationStateRef<char> state = new(in _context, tuple.data, ref array, tuple.meta);

            while (!state.remaining.IsEmpty)
                _ = RFCNEW<char>(ref state);
        }

        _context.ArrayPool.EnsureReturned(ref array);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlyMemory<T> RFCNEW<T>(ref CsvEnumerationStateRef<T> state) where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!state.remaining.IsEmpty);
        Debug.Assert(state._context.Dialect.Escape.HasValue);

        ReadOnlyMemory<T> field;
        ReadOnlySpan<T> span = state.remaining.Span;
        T quote = state._context.Dialect.Quote;
        T escape = state._context.Dialect.Escape.Value;
        T delimiter = state._context.Dialect.Delimiter;
        int consumed = 0;
        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        ref uint quotesRemaining = ref state.quotesRemaining;
        ref uint escapesRemaining = ref state.escapesRemaining;

        if (!state.isAtStart && !span[consumed++].Equals(delimiter))
        {
            state.ThrowNoDelimiterAtHead();
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
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(delimiter))
            {
                goto Done;
            }
        }

        goto EOL;

        NoQuotesHasEscapes:
        while (consumed < span.Length)
        {
            token = span.DangerousGetReferenceAt(consumed++);

            if (token.Equals(delimiter))
            {
                goto Done;
            }
            else if (token.Equals(escape))
            {
                if (consumed++ >= span.Length)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto NoQuotesNoEscapes;

                goto NoQuotesHasEscapes;
            }
        }

        goto EOL;

        HasQuotesNoEscapes:
        while (consumed < span.Length)
        {
            token = span.DangerousGetReferenceAt(consumed++);

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
        while (consumed < span.Length)
        {
            token = span.DangerousGetReferenceAt(consumed++);

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
                if (consumed++ >= span.Length)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto HasQuotesNoEscapes;

                goto HasQuotesAndEscapes;
            }
        }

        goto EOL;

        InStringNoEscapes:
        while (consumed < span.Length)
        {
            if (span.DangerousGetReferenceAt(consumed++).Equals(quote))
            {
                quotesConsumed++;

                if (--quotesRemaining == 0)
                    goto NoQuotesNoEscapes;

                goto HasQuotesNoEscapes;
            }
        }

        goto EOL;

        InStringWithEscapes:
        while (consumed < span.Length)
        {
            token = span.DangerousGetReferenceAt(consumed++);

            if (token.Equals(quote))
            {
                quotesConsumed++;

                if (--quotesRemaining == 0)
                    goto NoQuotesHasEscapes;

                goto HasQuotesAndEscapes;
            }
            else if (token.Equals(escape))
            {
                if (consumed++ >= span.Length)
                    state.ThrowEscapeAtEnd();

                escapesConsumed++;

                if (--escapesRemaining == 0)
                    goto InStringNoEscapes;

                goto InStringWithEscapes;
            }
        }

        EOL:
        if ((quotesRemaining | escapesRemaining) != 0)
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
        return (quotesConsumed | escapesConsumed) == 0
            ? field
            : EscapeMode<T>.Unescape(field, quote, escape, quotesConsumed, escapesConsumed, ref state.buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlyMemory<T> RFCOLD<T>(ref CsvEnumerationStateRef<T> state) where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!state.remaining.IsEmpty);
        Debug.Assert(state._context.Dialect.Escape.HasValue);

        ReadOnlySpan<T> remaining = state.remaining.Span;
        T delimiter = state._context.Dialect.Delimiter;
        T quote = state._context.Dialect.Quote;
        T escape = state._context.Dialect.Escape.Value;

        // If not the first field, validate that the first character is a delimiter
        if (!state.isAtStart)
        {
            // Since field count may not be known in advance, we leave the delimiter at the head after each field
            // so we can differentiate between an empty last field and end of the data in general
            if (!remaining[0].Equals(delimiter))
                state.ThrowNoDelimiterAtHead();

            state.remaining = state.remaining.Slice(1);
            remaining = remaining.Slice(1);
        }
        else
        {
            state.isAtStart = false;
        }

        // keep track of how many quotes the current field has
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
            // Hit a comma, either found end of field or more fields than expected
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
                    state.ThrowEscapeAtEnd();

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
                (_, 0) => quotesConsumed % 2 != 0 ? notYetRead.IndexOf(quote) : notYetRead.IndexOfAny(quote, delimiter),
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
            ? EscapeMode<T>.Unescape(field, quote, escape, quotesConsumed, escapesConsumed, ref state.buffer)
            : field;
    }
}
