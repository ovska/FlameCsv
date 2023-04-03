using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

/// <summary>
/// Reads CSV lines from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
internal static class LineReader
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

    /// <summary>
    /// Attempts to read until a non-string wrapped <see cref="CsvTokens{T}.NewLine"/> is found.
    /// </summary>
    /// <param name="tokens">Structural tokens instance from which newline and string delimiter tokens are used</param>
    /// <param name="sequence">
    /// Source data, modified if a newline is found and unmodified if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="line">
    /// The line without trailing newline tokens. Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="quoteCount">
    /// Count of string delimiters in <paramref name="line"/>, used when parsing the columns later on.
    /// Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="isFinalBlock">
    /// Whether the data is the last block in a given stream, and newline tokens need not be seeked.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <see cref="CsvTokens{T}.NewLine"/> was found, <paramref name="line"/>
    /// and <paramref name="quoteCount"/> can be used, and the line and newline have been sliced off from
    /// <paramref name="sequence"/>.
    /// </returns>
    /// <remarks>A successful result might still be invalid CSV.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetLine<T>(
        in CsvDialect<T> tokens,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount,
        bool isFinalBlock)
        where T : unmanaged, IEquatable<T>
    {
        return !isFinalBlock
            ? TryRead(in tokens, ref sequence, out line, out quoteCount)
            : TryGetFinalBlock(in tokens, ref sequence, out line, out quoteCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryRead<T>(
        in CsvDialect<T> tokens,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        ReadOnlySpan<T> newLine = tokens.Newline.Span;

        // keep track of read newline tokens and quotes in the read data
        State state = default;
        quoteCount = 0;

        // for iterating the sequence
        SequencePosition position = sequence.Start;
        SequencePosition current = position;

        while (sequence.TryGet(ref position, out ReadOnlyMemory<T> memory))
        {
            ReadOnlySpan<T> span = memory.Span;

            // Find the next relevant token. Uneven quotes mean the current index is 100% inside a string,
            // so we can skip everything until the next quote
            int index = quoteCount % 2 == 0
                ? span.IndexOfAny(newLine[state.count], tokens.Quote)
                : span.IndexOf(tokens.Quote);

            // Found a newline token or a string delimiter
            while (index >= 0)
            {
                // Found token was a string delimiter
                if (span[index].Equals(tokens.Quote))
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
                    ? span.Slice(index).IndexOfAny(newLine[state.count], tokens.Quote)
                    : span.Slice(index).IndexOf(tokens.Quote);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetFinalBlock<T>(
        in CsvDialect<T> tokens,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        if (sequence.IsEmpty)
        {
            line = default;
            quoteCount = default;
            return false;
        }

        line = sequence;

        if (line.IsSingleSegment)
        {
            quoteCount = line.FirstSpan.Count(tokens.Quote);
        }
        else
        {
            quoteCount = 0;

            foreach (var segment in sequence)
            {
                quoteCount += segment.Span.Count(tokens.Quote);
            }
        }

        sequence = default;
        return true;
    }
}
