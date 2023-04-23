using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Benchmark.Utils;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
public class ReadLineBench
{
    private static readonly CsvDialect<char> _dialect = CsvDialect<char>.Default;

    private readonly (ReadOnlyMemory<char> line, RecordMeta meta)[] _lines;

    private char[]? buffer;

    [GlobalCleanup]
    public void Cleanup()
    {
        ArrayPool<char>.Shared.EnsureReturned(ref buffer);
    }

    public ReadLineBench()
    {
        var text = File.ReadAllText(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.UTF8);

        var seq = new ReadOnlySequence<char>(text.AsMemory());

        _lines = new (ReadOnlyMemory<char> line, RecordMeta meta)[5000];
        int index = 0;

        while (RFC4180Mode<char>.TryGetLine(in _dialect, ref seq, out var line, out var meta))
        {
            Guard.IsTrue(line.IsSingleSegment);
            _lines[index++] = (line.First, meta);
        }

        Guard.IsEqualTo(index, 5000);
    }

    [Benchmark(Baseline = true)]
    public void Old()
    {
        foreach (ref readonly var tuple in _lines.AsSpan())
        {
            CsvEnumerationStateRef<char> state = new(
                in _dialect,
                tuple.line,
                tuple.line,
                true,
                tuple.meta,
                ref buffer,
                ArrayPool<char>.Shared,
                true);

            while (!state.remaining.IsEmpty)
            {
                _ = Mode<char>.Old(ref state);
            }
        }
    }

    [Benchmark]
    public void New()
    {
        foreach (ref readonly var tuple in _lines.AsSpan())
        {
            CsvEnumerationStateRef<char> state = new(
                in _dialect,
                tuple.line,
                tuple.line,
                true,
                tuple.meta,
                ref buffer,
                ArrayPool<char>.Shared,
                true);

            while (!state.remaining.IsEmpty)
            {
                _ = Mode<char>.New(ref state);
            }
        }
    }

    internal static class Mode<T> where T : unmanaged, IEquatable<T>
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyMemory<T> New(ref CsvEnumerationStateRef<T> state)
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
                    ThrowHelper.ThrowInvalidDataException();

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
                    state.remaining = state.remaining.Slice(index); // leave the comma in, see state.isAtStart
                    goto ReturnField;
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

            ReturnField:
            return quotesConsumed != 0
                ? RFC4180Mode<T>.Unescape(field, quote, quotesConsumed, ref state.buffer)
                : field;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyMemory<T> Old(ref CsvEnumerationStateRef<T> state)
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
                    ThrowHelper.ThrowInvalidDataException();

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
                        ? RFC4180Mode<T>.Unescape(field, quote, quotesConsumed, ref state.buffer)
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
                ? RFC4180Mode<T>.Unescape(field, quote, quotesConsumed, ref state.buffer)
                : field;
        }
    }
}
