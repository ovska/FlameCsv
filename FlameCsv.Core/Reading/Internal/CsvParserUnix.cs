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

    public override CsvRecordMeta GetRecordMeta(ReadOnlySpan<T> line)
    {
        ref readonly CsvDialect<T> dialect = ref Options.Dialect;
        T quote = dialect.Quote;
        T escape = dialect.Escape.GetValueOrDefault();

        int index = line.IndexOfAny(quote, escape);
        bool skipNext = false;

        CsvRecordMeta meta = default;

        if (index >= 0)
        {
            for (; index < line.Length; index++)
            {
                if (skipNext)
                {
                    skipNext = false;
                    continue;
                }

                if (line[index].Equals(quote))
                {
                    meta.quoteCount++;
                }
                else if (line[index].Equals(escape))
                {
                    meta.escapeCount++;
                    skipNext = true;
                }
            }
        }

        if (skipNext)
            ThrowForInvalidLastEscape(line, Options);

        if (meta.quoteCount == 1)
            ThrowForInvalidEscapeQuotes(line, Options);

        return meta;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override bool TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta)
    {
        CsvSequenceReader<T> copy = _reader;

        ReadOnlyMemory<T> unreadMemory = _reader.Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T start = ref remaining.DangerousGetReference();
        meta = default;

        while (!End)
        {
        Seek:
            int index = meta.quoteCount % 2 == 0
                ? remaining.IndexOfAny(_quote, _escape, _newline[0])
                : remaining.IndexOfAny(_quote, _escape);

            if (index != -1)
            {
                if (remaining[index].Equals(_quote))
                {
                    meta.quoteCount++;

                    if (_reader.AdvanceCurrent(index + 1))
                    {
                        remaining = _reader.Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 1);
                    }

                    goto Seek;
                }

                if (remaining[index].Equals(_escape))
                {
                    meta.escapeCount++;

                    // read past the escape and following
                    // use TryAdvance as the escape might be last token in the segment
                    if (!_reader.TryAdvance(index + 2, out bool refreshRemaining))
                        break;

                    if (refreshRemaining)
                    {
                        remaining = _reader.Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 2);
                    }

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

                if (_newline.Length == 1 || _reader.IsNext(_newline[1], advancePast: true))
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
                        line = unreadMemory.Slice(0, elementCount);

                        Debug.Assert(
                            _reader.Sequence.Slice(copy.Position, crPosition).SequenceEquals(line.Span),
                            $"Invalid slice: '{line}' vs '{_reader.Sequence.Slice(copy.Position, crPosition)}'");
                    }
                    else
                    {
                        line = _reader.Sequence.Slice(copy.Position, crPosition)
                            .AsMemory(Allocator, ref _multisegmentBuffer);
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

        CsvRecordMeta meta = default;

        while (linesRead < slices.Length)
        {
        Seek:
            int index = meta.quoteCount % 2 == 0
                ? data.IndexOfAny(_quote, _escape, _newline[0])
                : data.IndexOfAny(_quote, _escape);

            if (index < 0)
                break;

            if (_quote.Equals(data.DangerousGetReferenceAt(index)))
            {
                meta.quoteCount++;
                data = data.Slice(index + 1);
                currentConsumed += index + 1;
                goto Seek;
            }

            if (_escape.Equals(data.DangerousGetReferenceAt(index)))
            {
                // ran out of data
                if (index >= data.Length - 1)
                    break;

                meta.escapeCount++;
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
                if (!_newline[1].Equals(data.DangerousGetReferenceAt(index + 1)))
                {
                    data = data.Slice(index + 1);
                    currentConsumed++;
                    goto Seek;
                }
            }

            // Found newline
            slices[linesRead++] = new Slice { Index = consumed, Length = currentConsumed, Meta = meta, };

            consumed += currentConsumed + _newline.Length;
            data = data.Slice(index + _newline.Length);
            currentConsumed = 0;
            meta = default;
        }

        return (consumed, linesRead);
    }
}
