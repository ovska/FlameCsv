using System.Buffers;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
public class ModeLineBench
{
    private static readonly CsvReadingContext<byte> _context = new(CsvUtf8Options.Default);
    private static readonly ReadOnlySequence<byte> _bytes = new(File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv"));

    [Benchmark(Baseline = true)]
    public void Old()
    {
        ReadOnlySequence<byte> data = _bytes;
        var dialect = _context.Dialect;

        while (!data.IsEmpty)
        {
            _ = OLD(in dialect, ref data, out _, out _);
        }
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        ReadOnlySequence<byte> data = _bytes;
        var dialect = _context.Dialect;

        while (!data.IsEmpty)
        {
            _ = NEW(in dialect, ref data, out _, out _);
        }
    }

    /// <summary>Linefeed read state.</summary>
    private struct State
    {
        /// <summary>Count of newline tokens parsed before current index</summary>
        public int count;

        /// <summary>Index at <see cref="start"/> where the first newline token was found</summary>
        public int offset;

        /// <summary>Sequence position that was active when the first newline token was found</summary>
        public SequencePosition start;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool OLD<T>(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta)
        where T : unmanaged,IEquatable<T>
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

    /// <summary>
    /// Attempts to read until a non-string wrapped <see cref="CsvDialect{T}.Newline"/> is found.
    /// </summary>
    /// <param name="dialect">Structural tokens instance from which newline and string delimiter tokens are used</param>
    /// <param name="sequence">
    /// Source data, modified if a newline is found and unmodified if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="line">
    /// The line without trailing newline tokens. Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="meta">
    /// Count of string delimiters in <paramref name="line"/>, used when parsing the fields later on.
    /// Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <see cref="CsvDialect{T}.Newline"/> was found, <paramref name="line"/>
    /// and <paramref name="quoteCount"/> can be used, and the line and newline have been sliced off from
    /// <paramref name="sequence"/>.
    /// </returns>
    /// <remarks>A successful result might still be invalid CSV.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
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
            if (memory.Length == 0)
                goto NextSegment;

            ReadOnlySpan<T> span = memory.Span;
            ref T first = ref span.DangerousGetReference();
            nuint length = (uint)memory.Length;
            nuint index = 0;

            Seek:
            T nlt = newLine.DangerousGetReferenceAt(state.count);

            if (quoteCount % 2 == 0)
            {
                T lookUp;

                while ((length - index) >= 8)
                {
                    lookUp = Unsafe.Add(ref first, index);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found;
                    lookUp = Unsafe.Add(ref first, index + 1);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found1;
                    lookUp = Unsafe.Add(ref first, index + 2);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found2;
                    lookUp = Unsafe.Add(ref first, index + 3);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found3;
                    lookUp = Unsafe.Add(ref first, index + 4);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found4;
                    lookUp = Unsafe.Add(ref first, index + 5);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found5;
                    lookUp = Unsafe.Add(ref first, index + 6);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found6;
                    lookUp = Unsafe.Add(ref first, index + 7);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found7;

                    index += 8;
                }

                while ((length - index) >= 4)
                {
                    lookUp = Unsafe.Add(ref first, index);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found;
                    lookUp = Unsafe.Add(ref first, index + 1);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found1;
                    lookUp = Unsafe.Add(ref first, index + 2);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found2;
                    lookUp = Unsafe.Add(ref first, index + 3);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found3;
                    index += 4;
                }

                while (index < length)
                {
                    lookUp = Unsafe.Add(ref first, index);
                    if (nlt.Equals(lookUp) || quote.Equals(lookUp))
                        goto Found;
                    index++;
                }

            }
            else
            {
                while ((length - index) >= 8)
                {
                    if (quote.Equals(Unsafe.Add(ref first, index + 0)))
                        goto Found;
                    if (quote.Equals(Unsafe.Add(ref first, index + 1)))
                        goto Found1;
                    if (quote.Equals(Unsafe.Add(ref first, index + 2)))
                        goto Found2;
                    if (quote.Equals(Unsafe.Add(ref first, index + 3)))
                        goto Found3;
                    if (quote.Equals(Unsafe.Add(ref first, index + 4)))
                        goto Found4;
                    if (quote.Equals(Unsafe.Add(ref first, index + 5)))
                        goto Found5;
                    if (quote.Equals(Unsafe.Add(ref first, index + 6)))
                        goto Found6;
                    if (quote.Equals(Unsafe.Add(ref first, index + 7)))
                        goto Found7;
                    index += 8;
                }

                while ((length - index) >= 4)
                {
                    if (quote.Equals(Unsafe.Add(ref first, index + 0)))
                        goto Found;
                    if (quote.Equals(Unsafe.Add(ref first, index + 1)))
                        goto Found1;
                    if (quote.Equals(Unsafe.Add(ref first, index + 2)))
                        goto Found2;
                    if (quote.Equals(Unsafe.Add(ref first, index + 3)))
                        goto Found3;
                    index += 4;
                }

                while (index < length)
                {
                    if (quote.Equals(Unsafe.Add(ref first, index)))
                        goto Found;
                    index++;
                }
            }

            goto NextSegment;

            Found1:
            index += 1;
            goto Found;
            Found2:
            index += 2;
            goto Found;
            Found3:
            index += 3;
            goto Found;
            Found4:
            index += 4;
            goto Found;
            Found5:
            index += 5;
            goto Found;
            Found6:
            index += 6;
            goto Found;
            Found7:
            index += 7;
            goto Found;

            Found:

            // Found token was a string delimiter
            if (Unsafe.Add(ref first, index).Equals(quote))
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
                    state.offset = (int)index;
                    state.start = current;
                }

                // The conditions are:
                // - All of newline read, e.g. single LF or the CR was in previous segment
                // - Fast path check for common 2-token newline such as CRLF once CR was found
                // - Edge case of rest of a longer newline in a single segment
                // Even if 2&3 fail the line can still be valid if rest of the newline is in the next segment
                if (++state.count == newLine.Length
                    || (newLine.Length == 2 && index + 1 < length && Unsafe.Add(ref first, index + 1).Equals(newLine.DangerousGetReferenceAt(1)))
                    || span.Slice((int)index).StartsWith(newLine))
                {
                    SequencePosition newlinePos = sequence.GetPosition(state.offset, state.start);
                    line = sequence.Slice(0, newlinePos);
                    sequence = sequence.Slice(sequence.GetPosition(newLine.Length, newlinePos));
                    return true;
                }
            }

            // Move the cursor past the current token and exit if we hit the end of the segment
            if (++index < length)
            {
                goto Seek;
            }

            NextSegment:
            if (position.GetObject() is null)
                break;

            current = position;
        }

        Unsafe.SkipInit(out line); // keep this at the bottom to ensure successful returns actually set it
        return false;
    }
}
