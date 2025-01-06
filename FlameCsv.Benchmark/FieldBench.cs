using FlameCsv.Reading;
using FlameCsv.Extensions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;

namespace FlameCsv.Benchmark;

public class FieldBench
{
    private static readonly (string, CsvRecordMeta)[] _fields;

    static FieldBench()
    {
        using CsvParser<char> parser = CsvParser<char>.Create(CsvOptions<char>.Default);

        _fields = File
            .ReadAllLines("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv")
            .Select(l => (l, parser.GetRecordMeta(l)))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public void Old()
    {
        IMemoryOwner<char>? allocated = null;
        Span<char> buffer = stackalloc char[128];

        foreach (ref readonly var line in _fields.AsSpan())
        {
            var reader = new CsvFieldReader<char>(
                CsvOptions<char>.Default,
                line.Item1,
                buffer,
                ref allocated,
                in line.Item2);

            while (!reader.End)
                _ = Older<char>.ReadNextField(ref reader);
        }

        allocated?.Dispose();
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        IMemoryOwner<char>? allocated = null;
        Span<char> buffer = stackalloc char[128];

        foreach (ref readonly var line in _fields.AsSpan())
        {
            var reader = new CsvFieldReader<char>(
                CsvOptions<char>.Default,
                line.Item1,
                buffer,
                ref allocated,
                in line.Item2);

            while (!reader.End)
                _ = RFC4180Mode<char>.ReadNextField(ref reader);
        }

        allocated?.Dispose();
    }

    static class Older<T> where T : unmanaged, IEquatable<T>
    {
        public static ReadOnlySpan<T> ReadNextField(ref CsvFieldReader<T> state)
        {
            Debug.Assert(!state.End, "ReadNextField called with empty input");
            Debug.Assert(state.escapesRemaining == 0, "RFC4180 called with escapes in the input");

            ReadOnlySpan<T> field;
            T quote = state.Quote;
            T delimiter = state.Delimiter;
            nuint consumed = 0;
            uint quotesConsumed = 0;
            ref uint quotesRemaining = ref state.quotesRemaining;

            ref T first = ref state.GetRemainingRef(out nuint remaining);
            T lookUp;

            int sliceStart;

            if (!state.isAtStart)
            {
                if (!first.Equals(delimiter))
                {
                    state.ThrowNoDelimiterAtHead();
                }

                // delimiter is left at the start of data after the first field has been read
                consumed++;
                remaining--;
                sliceStart = 1;
            }
            else
            {
                sliceStart = 0;
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

            // consume the remaining
            field = state.Remaining.Slice(sliceStart);
            state.Consumed = state.Record.Length;
            goto Return;

            Done:
            int consumedi = (int)consumed;
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

                    if ((quotesConsumed -= 2) > 0)
                    {
                        Debug.Assert(quotesConsumed >= 2);
                        int unescapedLength = GetUnescapedLength(field.Length, quotesConsumed);
                        Span<T> unescapeBuffer = state.GetUnescapeBuffer(unescapedLength);

                        if (typeof(T) == typeof(char))
                        {
                            RFC4180Mode<ushort>.Unescape(
                                Unsafe.As<T, ushort>(ref quote),
                                unescapeBuffer.UnsafeCast<T, ushort>(),
                                field.UnsafeCast<T, ushort>(),
                                quotesConsumed);
                        }
                        else
                        {
                            RFC4180Mode<T>.Unescape(quote, unescapeBuffer, field, quotesConsumed);
                        }

                        field = unescapeBuffer;
                    }
                }
                else
                {
                    Throw();
                }
            }

            if (!state.Whitespace.IsEmpty)
                field = field.Trim(state.Whitespace);

            return field;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetUnescapedLength(int fieldLength, uint quotesRemaining)
        {
            return fieldLength - (int)(quotesRemaining / 2);
        }
    }

    private static void Throw() => throw new UnreachableException();
}
