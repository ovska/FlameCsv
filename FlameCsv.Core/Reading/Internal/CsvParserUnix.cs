using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserUnix<T> : CsvParser<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly T _escape;

    public CsvParserUnix(CsvOptions<T> options) : base(options)
    {
        Debug.Assert(options.Dialect.Escape.HasValue);
        _escape = options.Dialect.Escape.Value;
    }

    public override CsvLine<T> GetAsCsvLine(ReadOnlyMemory<T> line)
    {
        ref readonly CsvDialect<T> dialect = ref Options.Dialect;
        T quote = dialect.Quote;
        T escape = dialect.Escape.GetValueOrDefault();

        ReadOnlySpan<T> span = line.Span;
        int index = span.IndexOfAny(quote, escape);
        bool skipNext = false;

        uint quoteCount = 0;
        uint escapeCount = 0;

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
                    quoteCount++;
                }
                else if (span[index].Equals(escape))
                {
                    escapeCount++;
                    skipNext = true;
                }
            }
        }

        if (skipNext)
            ThrowForInvalidLastEscape(span, Options);

        if (quoteCount == 1)
            ThrowForInvalidEscapeQuotes(span, Options);

        return new CsvLine<T> { Value = line, QuoteCount = quoteCount, EscapeCount = escapeCount, };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override bool TryReadLine(out CsvLine<T> line)
    {
        CsvSequenceReader<T> copy = _reader;

        ReadOnlyMemory<T> unreadMemory = _reader.Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T start = ref remaining.DangerousGetReference();

        uint quoteCount = 0;
        uint escapeCount = 0;

        while (!End)
        {
        Seek:
            int index = quoteCount % 2 == 0
                ? remaining.IndexOfAny(_quote, _escape, _newline.First)
                : remaining.IndexOfAny(_quote, _escape);

            if (index != -1)
            {
                if (remaining[index].Equals(_quote))
                {
                    quoteCount++;

                    remaining = _reader.AdvanceCurrent(index + 1)
                        ? _reader.Unread.Span
                        : remaining.Slice(index + 1);

                    goto Seek;
                }

                if (remaining[index].Equals(_escape))
                {
                    escapeCount++;

                    // read past the escape and following
                    // use TryAdvance as the escape might be last token in the segment
                    if (!_reader.TryAdvance(index + 2, out bool refreshRemaining))
                        break;

                    remaining = refreshRemaining
                        ? _reader.Unread.Span
                        : remaining.Slice(index + 2);

                    goto Seek;
                }

                // must be newline token

                // Found one of the delimiters
                bool segmentHasChanged = index > 0 && _reader.AdvanceCurrent(index);

                // store first newline token position
                var crPosition = _reader.Position;

                // and advance past it
                if (_reader.AdvanceCurrent(1))
                    segmentHasChanged = true;

                if (_newline.Length == 1 || _reader.IsNext(_newline.Second, advancePast: true))
                {
                    // perf: non-empty slice from the same segment, read directly from the original memory
                    // at this point, index points to the first newline token
                    if (copy.Position.GetObject() == crPosition.GetObject()
                        && (unreadMemory.Length | remaining.Length) != 0)
                    {
                        int byteOffset = (int)Unsafe.ByteOffset(
                            ref start,
                            ref remaining.DangerousGetReferenceAt(index));
                        int elementCount = byteOffset / Unsafe.SizeOf<T>();
                        line = new()
                        {
                            Value = unreadMemory.Slice(0, elementCount),
                            QuoteCount = quoteCount,
                            EscapeCount = escapeCount,
                        };

                        Debug.Assert(
                            _reader.Sequence.Slice(copy.Position, crPosition).SequenceEquals(line.Value.Span),
                            $"Invalid slice: '{line}' vs '{_reader.Sequence.Slice(copy.Position, crPosition)}'");
                    }
                    else
                    {
                        line = new()
                        {
                            QuoteCount = quoteCount,
                            EscapeCount = escapeCount,
                            Value = _reader.Sequence.Slice(copy.Position, crPosition)
                                .AsMemory(Allocator, ref _multisegmentBuffer),
                        };
                    }

                    return true;
                }

                if (!_reader.TryAdvance(1, out bool segmentChanged))
                    break;

                if (segmentChanged || segmentHasChanged)
                    remaining = _reader.Unread.Span;

                goto Seek;
            }

            _reader.AdvanceCurrent(remaining.Length);
            remaining = _reader.Current.Span;
        }

        // Didn't find anything, reset our original state.
        _reader = copy;
        Unsafe.SkipInit(out line);
        return false;
    }

    protected override (int consumed, int linesRead) FillSliceBuffer(ReadOnlySpan<T> data, Span<Slice> slices)
    {
        Debug.Assert(_newline.Length != 0);

        int linesRead = 0;
        int consumed = 0;
        int currentConsumed = 0;

        uint quoteCount = 0;
        uint escapeCount = 0;

        while (linesRead < slices.Length)
        {
        Seek:
            int index = quoteCount % 2 == 0
                ? data.IndexOfAny(_quote, _escape, _newline.First)
                : data.IndexOfAny(_quote, _escape);

            if (index < 0)
                break;

            if (_quote.Equals(data.DangerousGetReferenceAt(index)))
            {
                quoteCount++;
                data = data.Slice(index + 1);
                currentConsumed += index + 1;
                goto Seek;
            }

            if (_escape.Equals(data.DangerousGetReferenceAt(index)))
            {
                // ran out of data
                if (index >= data.Length - 1)
                    break;

                escapeCount++;
                data = data.Slice(index + 2);
                currentConsumed += index + 2;
                goto Seek;
            }

            currentConsumed += index;

            if (_newline.Length == 2)
            {
                // ran out of data
                if (index >= data.Length - 1)
                    break;

                // next token wasn't the second newline
                if (!_newline.Second.Equals(data.DangerousGetReferenceAt(index + 1)))
                {
                    data = data.Slice(index + 1);
                    currentConsumed++;
                    goto Seek;
                }
            }

            // Found newline
            slices[linesRead++] = new Slice
            {
                Index = consumed, Length = currentConsumed, QuoteCount = quoteCount, EscapeCount = escapeCount,
            };

            consumed += currentConsumed + _newline.Length;
            data = data.Slice(index + _newline.Length);
            currentConsumed = 0;
            quoteCount = 0;
            escapeCount = 0;
        }

        return (consumed, linesRead);
    }
}
