using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Reading;

namespace FlameCsv.Extensions;

internal static class ReadingExtensions
{
    /// <summary>
    /// Seeks the sequence for a <see cref="CsvDialect{T}.Newline"/>.
    /// </summary>
    /// <param name="dialect">Dialect that determines the quote, newline, and escape</param>
    /// <param name="sequence">
    /// Source data, modified if a newline is found and unmodified if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="line">
    /// The line without trailing newline tokens. Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="meta">
    /// Line metadata useful when parsing the line later. Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="isFinalBlock">
    /// Whether no more data is expected, and the sequence is not expected to have a trailing newline.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <see cref="CsvDialect{T}.Newline"/> was found, <paramref name="line"/>
    /// and <paramref name="quoteCount"/> can be used, and the line and newline have been sliced off from
    /// <paramref name="sequence"/>.
    /// </returns>
    /// <remarks>A successful result might still be invalid CSV.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetLine<T>(
        this in CsvDialect<T> dialect,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta,
        bool isFinalBlock)
        where T : unmanaged, IEquatable<T>
    {
        if (!isFinalBlock)
        {
            return !dialect.Escape.HasValue
                ? RFC4180Mode<T>.TryGetLine(in dialect, ref sequence, out line, out meta)
                : EscapeMode<T>.TryGetLine(in dialect, ref sequence, out line, out meta);
        }

        if (!sequence.IsEmpty)
        {
            line = sequence;
            meta = dialect.GetRecordMeta(in sequence, exposeContent: false);
            sequence = default;
            return true;
        }

        line = default;
        meta = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetField<T>(this ref CsvEnumerationStateRef<T> state, out ReadOnlyMemory<T> line)
        where T : unmanaged, IEquatable<T>
    {
        return !state.Dialect.Escape.HasValue
            ? RFC4180Mode<T>.TryGetField(ref state, out line)
            : EscapeMode<T>.TryGetField(ref state, out line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> ReadNextField<T>(this ref CsvEnumerationStateRef<T> state)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!state.remaining.IsEmpty);
        return !state.Dialect.Escape.HasValue
            ? RFC4180Mode<T>.ReadNextField(ref state)
            : EscapeMode<T>.ReadNextField(ref state);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RecordMeta GetRecordMeta<T>(
        this in CsvDialect<T> dialect,
        ReadOnlyMemory<T> memory,
        bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        RecordMeta meta = default;

        if (dialect.IsRFC4188Mode)
        {
            meta.quoteCount = (uint)memory.Span.Count(dialect.Quote);

            if (meta.quoteCount % 2 != 0)
                ThrowForUnevenQuotes(in dialect, memory, exposeContent);
        }
        else
        {
            bool skipNext = false;

            CountTokensEscape(memory.Span, in dialect, ref meta, ref skipNext);

            if (skipNext)
                ThrowForInvalidLastEscape(in dialect, memory, exposeContent);

            if (meta.quoteCount == 1)
                ThrowForInvalidEscapeQuotes(in dialect, memory, exposeContent);
        }

        return meta;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RecordMeta GetRecordMeta<T>(
        this in CsvDialect<T> dialect,
        in ReadOnlySequence<T> sequence,
        bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        if (sequence.IsSingleSegment)
            return GetRecordMeta(in dialect, sequence.First, exposeContent);

        RecordMeta meta = default;

        if (dialect.IsRFC4188Mode)
        {
            foreach (var segment in sequence)
            {
                meta.quoteCount += (uint)segment.Span.Count(dialect.Quote);
            }

            if (meta.quoteCount % 2 != 0)
                ThrowForUnevenQuotes(in dialect, in sequence, exposeContent);

            return meta;
        }

        bool skipNext = false;

        foreach (var segment in sequence)
        {
            if (!segment.IsEmpty)
                CountTokensEscape(segment.Span, in dialect, ref meta, ref skipNext);
        }

        if (skipNext)
            ThrowForInvalidLastEscape(in dialect, in sequence, exposeContent);

        if (meta.quoteCount == 1)
            ThrowForInvalidEscapeQuotes(in dialect, in sequence, exposeContent);

        return meta;
    }

    private static void CountTokensEscape<T>(
        ReadOnlySpan<T> span,
        in CsvDialect<T> dialect,
        ref RecordMeta meta,
        ref bool skipNext)
        where T : unmanaged, IEquatable<T>
    {
        T quote = dialect.Quote;
        T escape = dialect.Escape.GetValueOrDefault();

        int index = span.IndexOfAny(quote, escape);

        if (index >= 0)
        {
            for (; index < span.Length; index++)
            {
                if (skipNext)
                {
                    skipNext = false;
                    continue;
                }

                if (span[index].Equals(quote))
                {
                    meta.quoteCount++;
                }
                else if (span[index].Equals(escape))
                {
                    meta.escapeCount++;
                    skipNext = true;
                }
            }
        }
        else
        {
            skipNext = false;
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidLastEscape<T>(in CsvDialect<T> dialect, in ReadOnlySequence<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForInvalidLastEscape(in dialect, view.Memory, exposeContent);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidLastEscape<T>(in CsvDialect<T> dialect, ReadOnlyMemory<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"The final entry ended on a escape character: {line.Span.AsPrintableString(exposeContent, in dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidEscapeQuotes<T>(in CsvDialect<T> dialect, in ReadOnlySequence<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForInvalidEscapeQuotes(in dialect, view.Memory, exposeContent);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalidEscapeQuotes<T>(in CsvDialect<T> dialect, ReadOnlyMemory<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"The entry had an invalid amount of quotes for escaped CSV: {line.Span.AsPrintableString(exposeContent, in dialect)}");
    }
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForUnevenQuotes<T>(in CsvDialect<T> dialect, in ReadOnlySequence<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForUnevenQuotes(in dialect, view.Memory, exposeContent);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForUnevenQuotes<T>(in CsvDialect<T> dialect, ReadOnlyMemory<T> line, bool exposeContent)
        where T : unmanaged, IEquatable<T>
    {
        throw new ArgumentException(
            $"The data had an uneven amount of quotes : {line.Span.AsPrintableString(exposeContent, in dialect)}");
    }
}
