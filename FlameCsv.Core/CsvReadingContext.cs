﻿using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal readonly struct CsvReadingContext<T> where T : unmanaged, IEquatable<T>
{
    public readonly CsvDialect<T> dialect;
    public readonly ArrayPool<T> arrayPool;
    public readonly bool hasHeader;
    public readonly bool exposeContent;
    public readonly bool validateFieldCount;
    private readonly CsvCallback<T, bool>? _skipCallback;
    private readonly CsvExceptionHandler<T>? _exceptionHandler;

    public CsvReadingContext(CsvReaderOptions<T> options)
    {
        dialect = new CsvDialect<T>(options);
        arrayPool = options.ArrayPool.AllocatingIfNull();
        hasHeader = options.HasHeader;
        exposeContent = options.AllowContentInExceptions;
        validateFieldCount = options.ValidateFieldCount;
        _skipCallback = options.ShouldSkipRow;
        _exceptionHandler = options.ExceptionHandler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SkipRecord(ReadOnlyMemory<T> value) => _skipCallback?.Invoke(value, in dialect) ?? false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExceptionIsHandled(ReadOnlyMemory<T> value, Exception exception) => (_exceptionHandler?.Invoke(value, exception)) ?? false;

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
    public bool TryGetLine(
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out RecordMeta meta,
        bool isFinalBlock)
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
            meta = GetRecordMeta(in sequence);
            sequence = default;
            return true;
        }

        line = default;
        meta = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetField(ref CsvEnumerationStateRef<T> state, out ReadOnlyMemory<T> line)
    {
        return !dialect.Escape.HasValue
            ? RFC4180Mode<T>.TryGetField(ref state, out line)
            : EscapeMode<T>.TryGetField(ref state, out line);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> ReadNextField(ref CsvEnumerationStateRef<T> state)
    {
        Debug.Assert(!state.remaining.IsEmpty);
        return !dialect.Escape.HasValue
            ? RFC4180Mode<T>.ReadNextField(ref state)
            : EscapeMode<T>.ReadNextField(ref state);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public RecordMeta GetRecordMeta(ReadOnlyMemory<T> memory)
    {
        RecordMeta meta = default;

        if (dialect.IsRFC4188Mode)
        {
            meta.quoteCount = (uint)memory.Span.Count(dialect.Quote);

            if (meta.quoteCount % 2 != 0)
                ThrowForUnevenQuotes(memory);
        }
        else
        {
            bool skipNext = false;

            CountTokensEscape(memory.Span, ref meta, ref skipNext);

            if (skipNext)
                ThrowForInvalidLastEscape(memory);

            if (meta.quoteCount == 1)
                ThrowForInvalidEscapeQuotes(memory);
        }

        return meta;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public RecordMeta GetRecordMeta(in ReadOnlySequence<T> sequence)
    {
        if (sequence.IsSingleSegment)
            return GetRecordMeta(sequence.First);

        RecordMeta meta = default;

        if (dialect.IsRFC4188Mode)
        {
            foreach (var segment in sequence)
            {
                meta.quoteCount += (uint)segment.Span.Count(dialect.Quote);
            }

            if (meta.quoteCount % 2 != 0)
                ThrowForUnevenQuotes(in sequence);

            return meta;
        }

        bool skipNext = false;

        foreach (var segment in sequence)
        {
            if (!segment.IsEmpty)
                CountTokensEscape(segment.Span, ref meta, ref skipNext);
        }

        if (skipNext)
            ThrowForInvalidLastEscape(in sequence);

        if (meta.quoteCount == 1)
            ThrowForInvalidEscapeQuotes(in sequence);

        return meta;
    }

    private void CountTokensEscape(
        ReadOnlySpan<T> span,
        ref RecordMeta meta,
        ref bool skipNext)
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
    private void ThrowForInvalidLastEscape(in ReadOnlySequence<T> line)
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForInvalidLastEscape(view.Memory);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowForInvalidLastEscape(ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The final entry ended on a escape character: {AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowForInvalidEscapeQuotes(in ReadOnlySequence<T> line)
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForInvalidEscapeQuotes(view.Memory);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowForInvalidEscapeQuotes(ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The entry had an invalid amount of quotes for escaped CSV: {AsPrintableString(line)}");
    }
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowForUnevenQuotes(in ReadOnlySequence<T> line)
    {
        using SequenceView<T> view = new(in line, ArrayPool<T>.Shared, clearArray: true);
        ThrowForUnevenQuotes(view.Memory);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public void ThrowForUnevenQuotes(ReadOnlyMemory<T> line)
    {
        throw new ArgumentException(
            $"The data had an uneven amount of quotes: {AsPrintableString(line)}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string AsPrintableString(ReadOnlyMemory<T> value) => AsPrintableString(value.Span);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string AsPrintableString(ReadOnlySpan<T> value)
    {
        string? content =
            !exposeContent ? null :
            typeof(T) == typeof(char) ? value.ToString() :
            typeof(T) == typeof(byte) ? Encoding.UTF8.GetString(value.Cast<T, byte>()) :
            null;

        using var memoryOwner = MemoryOwner<T>.Allocate(value.Length);
        value.CopyTo(memoryOwner.Span);

        string structure = string.Create(
            value.Length,
            (dialect, memoryOwner.Memory),
            static (destination, state) =>
            {
                (CsvDialect<T> dialect, ReadOnlyMemory<T> memory) = state;
                var source = memory.Span;

                var newline = dialect.Newline.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = source[i];

                    if (token.Equals(dialect.Delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(dialect.Quote))
                    {
                        destination[i] = '"';
                    }
                    else if (dialect.Escape.HasValue && token.Equals(dialect.Escape.Value))
                    {
                        destination[i] = 'E';
                    }
                    else if (newline.Contains(token))
                    {
                        destination[i] = 'N';
                    }
                }
            });

        if (content is null)
            return $"Data structure: [{structure}]";

        return $"Content: [{content}], data structure: [{structure}]";
    }
}
