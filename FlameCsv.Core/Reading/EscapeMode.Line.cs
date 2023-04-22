using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

internal interface ICsvMode<T> where T : unmanaged, IEquatable<T>
{
    bool TryGetLine(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta);

    ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state);
}

internal static partial class EscapeMode<T> where T : unmanaged, IEquatable<T>
{
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
    public static bool TryGetLine(
        in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta)
    {
        Debug.Assert(dialect.Escape.HasValue, "EscapeMode is only valid with an usable escape character.");

        ReadOnlySpan<T> newLine = dialect.Newline.Span;
        T quote = dialect.Quote;
        T escape = dialect.Escape!.Value;

        meta = default;
        ref uint quoteCount = ref meta.quoteCount;
        ref uint escapeCount = ref meta.escapeCount;

        // keep track of read newline tokens and quotes in the read data
        State state = default;

        // 1 = previous token was an escape
        int skip = 0;

        // for iterating the sequence
        SequencePosition position = sequence.Start;
        SequencePosition current = position;

        while (sequence.TryGet(ref position, out ReadOnlyMemory<T> memory))
        {
            if (memory.IsEmpty)
            {
                goto NextSegment;
            }
            // single segment segment that needs to be skipped
            else if (memory.Length == skip)
            {
                skip = 0;
                goto NextSegment;
            }

            ReadOnlySpan<T> span = memory.Span;

            // Find the next relevant token. Uneven quotes mean the current index is 100% inside a string,
            // so we can skip everything until the next quote
            // If we need to skip the first token in the segment, ignore the first character and increment the
            // result of IndexOf. This is safe because we've already established above that the segment isn't empty
            int index = quoteCount % 2 == 0
                ? span.Slice(skip).IndexOfAny(newLine[state.count], quote, escape)
                : span.Slice(skip).IndexOfAny(quote, escape);

            if (index >= 0)
                index += skip;

            skip = 0;

            // Found a newline token or a string delimiter
            while (index >= 0)
            {
                if (span[index].Equals(quote))
                {
                    quoteCount++;
                    state = default; // reset possible newline state
                }
                else if (span[index].Equals(escape))
                {
                    skip = 1;
                    escapeCount++;
                    state = default; // reset possible newline state
                }
                // The match was for newline token
                else
                {
                    // We are 100% not inside a string as newline tokens are ignored by IndexOf in that case
                    Debug.Assert(quoteCount % 2 == 0);
                    Debug.Assert(span[index].Equals(newLine[state.count]));

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

                if (skip != 0)
                {
                    skip = 0;

                    if (++index >= span.Length)
                        break;
                }

                // Find the next relevant token
                int next = quoteCount % 2 == 0
                    ? span.Slice(index).IndexOfAny(newLine[state.count], quote, escape)
                    : span.Slice(index).IndexOfAny(quote, escape);

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

            NextSegment:
            // Move to the next segment, and abort if it was the last one
            if (position.GetObject() is null)
                break;

            current = position;
        }

        Unsafe.SkipInit(out line); // keep this at the bottom to ensure successful returns actually set it
        return false;
    }
}
