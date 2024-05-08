using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
public class ModeBench2
{
    private static readonly CsvReadingContext<char> _context = new(CsvTextOptions.Default);
    private static readonly ReadOnlySequence<char> _data = new ReadOnlySequence<char>(
        Encoding.UTF8.GetChars(File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv")));

    [Benchmark(Baseline = true)]
    public void Old()
    {
        ReadOnlySequence<char> data = _data;
        CsvDialect<char> dialect = _context.Dialect;

        while (OLD(in dialect, ref data, out _, out _))
        {
        }
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        ReadOnlySequence<char> data = _data;
        CsvDialect<char> dialect = _context.Dialect;

        while (NEW(in dialect, ref data, out _, out _))
        {
        }
    }
    private static bool NEW<T>(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta)
        where T : unmanaged, IEquatable<T>
    {
        ReadOnlySpan<T> newLine = dialect.Newline.Span;
        T quote = dialect.Quote;
        meta = default;
        ref uint quoteCount = ref meta.quoteCount;

        // keep track of read newline tokens and quotes in the read data
        State state = default;

        // for iterating the sequence
        SequencePosition position = sequence.Start;
        SequencePosition current = position;

        while (sequence.TryGet(ref position, out ReadOnlyMemory<T> memory))
        {
            ReadOnlySpan<T> span = memory.Span;
            ref T first = ref MemoryMarshal.GetReference(span);

            int consumed = 0;

            if (state.count == 0)
                goto MainLoop;

            goto SeekNewline;

            FoundNewline:
            SequencePosition newlinePos = sequence.GetPosition(state.offset, state.start);
            line = sequence.Slice(0, newlinePos);
            sequence = sequence.Slice(sequence.GetPosition(newLine.Length, newlinePos));
            return true;

            SeekNewline:
            while (consumed < span.Length)
            {
                T token = Unsafe.Add(ref first, consumed);

                if (token.Equals(newLine[state.count]))
                {
                    state.count++;
                    consumed++;

                    if (state.count == newLine.Length)
                    {
                        goto FoundNewline;
                    }

                    continue;
                }

                if (token.Equals(quote))
                {
                    quoteCount++;
                }

                state = default;
                consumed++;

            }

            MainLoop:
            while (consumed < span.Length)
            {
                int index = span.Slice(consumed).IndexOf(newLine[0]);

                // quote count goes here
                {
                    nint remaining = index == -1 ? span.Length - consumed : index;
                    nint offset = consumed;

                    nint res0 = 0;
                    nint res1 = 0;
                    nint res2 = 0;
                    nint res3 = 0;
                    nint res4 = 0;
                    nint res5 = 0;
                    nint res6 = 0;
                    nint res7 = 0;

                    // Main loop with 8 unrolled iterations
                    while (remaining >= 8)
                    {
                        res0 += Unsafe.Add(ref first, offset + 0).Equals(quote).ToByte();
                        res1 += Unsafe.Add(ref first, offset + 1).Equals(quote).ToByte();
                        res2 += Unsafe.Add(ref first, offset + 2).Equals(quote).ToByte();
                        res3 += Unsafe.Add(ref first, offset + 3).Equals(quote).ToByte();
                        res4 += Unsafe.Add(ref first, offset + 4).Equals(quote).ToByte();
                        res5 += Unsafe.Add(ref first, offset + 5).Equals(quote).ToByte();
                        res6 += Unsafe.Add(ref first, offset + 6).Equals(quote).ToByte();
                        res7 += Unsafe.Add(ref first, offset + 7).Equals(quote).ToByte();

                        remaining -= 8;
                        offset += 8;
                    }

                    if (remaining >= 4)
                    {
                        res0 += Unsafe.Add(ref first, offset + 0).Equals(quote).ToByte();
                        res1 += Unsafe.Add(ref first, offset + 1).Equals(quote).ToByte();
                        res2 += Unsafe.Add(ref first, offset + 2).Equals(quote).ToByte();
                        res3 += Unsafe.Add(ref first, offset + 3).Equals(quote).ToByte();

                        remaining -= 4;
                        offset += 4;
                    }

                    // Iterate over the remaining values and count those that match
                    while (remaining > 0)
                    {
                        res0 += Unsafe.Add(ref first, offset).Equals(quote).ToByte();

                        remaining -= 1;
                        offset += 1;
                    }

                    quoteCount += (uint)(res0 + res1 + res2 + res3 + res4 + res5 + res6 + res7);
                }

                if (index == -1)
                {
                    state = default;
                    break;
                }

                // Not inside a string
                if (quoteCount % 2 == 0)
                {
                    // init the newline state if this was the first token
                    if (state.count == 0)
                    {
                        state.offset = consumed + index;
                        state.start = current;
                    }

                    state.count++;

                    if (state.count == newLine.Length)
                    {
                        goto FoundNewline;
                    }

                    consumed += index + 1;
                    goto SeekNewline;
                }

                // Move the cursor past the current token and exit if we hit the end of the segment
                consumed += index + 1;
            }

            if (position.GetObject() is null)
                break;

            current = position;
        }

        Unsafe.SkipInit(out line); // keep this at the bottom to ensure successful returns actually set it
        return false;
    }

    private static bool OLD<T>(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta)
        where T : unmanaged, IEquatable<T>
    {
        ReadOnlySpan<T> newLine = dialect.Newline.Span;
        T quote = dialect.Quote;
        meta = default;
        ref uint quoteCount = ref meta.quoteCount;

        // keep track of read newline tokens and quotes in the read data
        State state = default;

        // for iterating the sequence
        SequencePosition position = sequence.Start;
        SequencePosition current = position;

        while (sequence.TryGet(ref position, out ReadOnlyMemory<T> memory))
        {
            ReadOnlySpan<T> span = memory.Span;

            // Find the next relevant token. Uneven quotes mean the current index is 100% inside a string,
            // so we can skip everything until the next quote
            int index = quoteCount % 2 == 0
                ? span.IndexOfAny(newLine[state.count], quote)
                : span.IndexOf(quote);

            // Found a newline token or a string delimiter
            while (index >= 0)
            {
                // Found token was a string delimiter
                if (span[index].Equals(quote))
                {
                    quoteCount++;
                    state = default; // zero out possible newline state such as \r"
                }
                // The match was for newline token
                else
                {
                    // We are 100% not inside a string as newline tokens are ignored by IndexOf in that case
                    Debug.Assert(quoteCount % 2 == 0);

                    // init the newline state if this was the first token
                    if (state.count == 0)
                    {
                        state.offset = index;
                        state.start = current;
                    }

                    // The conditions are:
                    // - All of newline read, e.g. single LF or the CR was in previous segment
                    // - Fast path check for common 2-token newline such as CRLF once CR was found
                    // - Edge case of rest of a longer newline in a single segment
                    // Even if 2&3 fail the line can still be valid if rest of the newline is in the next segment
                    if (++state.count == newLine.Length
                        || (newLine.Length == 2 && index + 1 < span.Length && span[index + 1].Equals(newLine[1]))
                        || span.Slice(index).StartsWith(newLine))
                    {
                        SequencePosition newlinePos = sequence.GetPosition(state.offset, state.start);
                        line = sequence.Slice(0, newlinePos);
                        sequence = sequence.Slice(sequence.GetPosition(newLine.Length, newlinePos));
                        return true;
                    }
                }

                // Move the cursor past the current token and exit if we hit the end of the segment
                if (++index >= span.Length)
                    break;

                // Find the next relevant token
                int next = quoteCount % 2 == 0
                    ? span.Slice(index).IndexOfAny(newLine[state.count], quote)
                    : span.Slice(index).IndexOf(quote);

                // The segment still contains something of interest
                if (next >= 0)
                {
                    index += next;
                }
                // No string or newline tokens in the segment, try to move to next
                else
                {
                    break;
                }
            }

            if (position.GetObject() is null)
                break;

            current = position;
        }

        Unsafe.SkipInit(out line); // keep this at the bottom to ensure successful returns actually set it
        return false;
    }

    /// <summary>Linefeed read state.</summary>
    private ref struct State
    {
        /// <summary>Count of newline tokens parsed before current index</summary>
        public int count;

        /// <summary>Index at <see cref="start"/> where the first newline token was found</summary>
        public int offset;

        /// <summary>Sequence position that was active when the first newline token was found</summary>
        public SequencePosition start;
    }
}
