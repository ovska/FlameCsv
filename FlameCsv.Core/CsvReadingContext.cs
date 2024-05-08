using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// An immutable configuration instance used in a single CSV reading operation.
/// </summary>
internal readonly struct CsvReadingContext<T> where T : unmanaged, IEquatable<T>
{
    public void EnsureValid()
    {
        if (Options is null)
            Throw.InvalidOp_DefaultStruct(typeof(CsvReadingContext<T>));
    }

    public readonly CsvDialect<T> Dialect;
    public readonly CsvOptions<T> Options;

    public ArrayPool<T> ArrayPool => Options._arrayPool.AllocatingIfNull();
    public bool HasHeader => Options._hasHeader;
    public bool ExposeContent => Options._allowContentInExceptions;
    public bool ValidateFieldCount => Options._validateFieldCount;

    public CsvReadingContext(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        Options = options;
        Dialect = new CsvDialect<T>(options);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SkipRecord(ReadOnlyMemory<T> record, int line, bool headerRead)
    {
        return Options._shouldSkipRow is { } predicate && predicate(new CsvRecordSkipArgs<T>
        {
            Options = Options,
            Line = line,
            Record = record,
            HeaderRead = headerRead,
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExceptionIsHandled(ReadOnlyMemory<T> record, int line, Exception exception)
    {
        return Options._exceptionHandler is { } handler && handler(new CsvExceptionHandlerArgs<T>
        {
            Options = Options,
            Line = line,
            Record = record,
            Exception = exception,
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(
        CsvDataReader<T> data,
        out ReadOnlyMemory<T> line,
        out RecordMeta meta,
        bool isFinalBlock)
    {
        if (!isFinalBlock)
        {
            LineSeekArg<T> arg = new(in this, ref data.MultisegmentBuffer);
            return data.Reader.TryReadLine(arg, out line, out meta);
        }

        if (!data.Reader.End)
        {
            return TryGetRecordMeta(data, out line, out meta);
        }

        Unsafe.SkipInit(out line);
        Unsafe.SkipInit(out meta);
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public RecordMeta GetRecordMeta(ReadOnlyMemory<T> memory)
    {
        RecordMeta meta = default;

        if (Dialect.IsRFC4180Mode)
        {
            meta.quoteCount = (uint)memory.Span.Count(Dialect.Quote);

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
    public RecordMeta GetRecordMeta(ref ReadOnlySequence<T> sequence, out ReadOnlySequence<T> line)
    {
        RecordMeta meta;

        if (sequence.IsSingleSegment)
        {
            meta = GetRecordMeta(sequence.First);
        }
        else
        {
            meta = default;

            if (Dialect.IsRFC4180Mode)
            {
                foreach (var segment in sequence)
                {
                    meta.quoteCount += (uint)segment.Span.Count(Dialect.Quote);
                }

                if (meta.quoteCount % 2 != 0)
                    ThrowForUnevenQuotes(in sequence);
            }
            else
            {

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
            }
        }

        line = sequence;
        sequence = default;
        return meta;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryGetRecordMeta(CsvDataReader<T> data, out ReadOnlyMemory<T> line, out RecordMeta meta)
    {
        if (data.Reader.End)
        {
            Unsafe.SkipInit(out line);
            Unsafe.SkipInit(out meta);
            return false;
        }

        var sequence = data.Reader.UnreadSequence;

        if (sequence.IsSingleSegment)
        {
            meta = GetRecordMeta(sequence.First);
        }
        else
        {
            meta = default;

            if (Dialect.IsRFC4180Mode)
            {
                foreach (var segment in sequence)
                {
                    meta.quoteCount += (uint)segment.Span.Count(Dialect.Quote);
                }

                if (meta.quoteCount % 2 != 0)
                    ThrowForUnevenQuotes(in sequence);
            }
            else
            {

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
            }
        }

        line = sequence.AsMemory(ref data.MultisegmentBuffer, ArrayPool);
        data.Reader.AdvanceToEnd();
        return true;
    }

    public TValue Materialize<TValue>(ReadOnlyMemory<T> record, IMaterializer<T, TValue> value)
    {
        T[]? array = null;

        try
        {
            var meta = GetRecordMeta(record);
            CsvFieldReader<T> reader = new(
                record,
                in this,
                stackalloc T[Token<T>.StackLength],
                ref array,
                meta.quoteCount,
                meta.escapeCount);

            return value.Parse(ref reader);
        }
        finally
        {
            ArrayPool.EnsureReturned(ref array);
        }
    }

    private void CountTokensEscape(
        ReadOnlySpan<T> span,
        ref RecordMeta meta,
        ref bool skipNext)
    {
        T quote = Dialect.Quote;
        T escape = Dialect.Escape.GetValueOrDefault();

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
        throw new CsvFormatException($"The record ended with an escape character: {AsPrintableString(line)}");
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
    public string AsPrintableString(ReadOnlyMemory<T> value)
    {
        string? content = ExposeContent ? Options.GetAsString(value.Span) : null;

        string structure = string.Create(
            length: value.Length,
            state: (Dialect, value),
            action: static (destination, state) =>
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

