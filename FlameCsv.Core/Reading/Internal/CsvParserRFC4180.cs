using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserRFC4180<T> : CsvParser<T> where T : unmanaged, IBinaryInteger<T>
{
    public CsvParserRFC4180(CsvOptions<T> options) : base(options)
    {
    }

    public override CsvRecordMeta GetRecordMeta(ReadOnlySpan<T> line)
    {
        CsvRecordMeta meta = new() { quoteCount = (uint)System.MemoryExtensions.Count(line, Options.Dialect.Quote), };

        if (meta.quoteCount % 2 != 0)
            ThrowForUnevenQuotes(line, Options);

        return meta;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override bool TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta)
    {
        CsvSequenceReader<T> copy = _reader;

        ReadOnlyMemory<T> unreadMemory = _reader.Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T start = ref remaining.DangerousGetReference();

        meta = new();

        while (!_reader.End)
        {
        Seek:
            int index = meta.quoteCount % 2 == 0
                ? remaining.IndexOfAny(_quote, _newline[0])
                : remaining.IndexOf(_quote);

            if (index != -1)
            {
                if (remaining[index] == _quote)
                {
                    meta.quoteCount++;

                    remaining = _reader.AdvanceCurrent(index + 1)
                        ? _reader.Unread.Span
                        : remaining.Slice(index + 1);

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override (int consumed, int linesRead) FillSliceBuffer(ReadOnlySpan<T> data, Span<Slice> slices)
    {
        int linesRead = 0;
        int consumed = 0;
        int currentConsumed = 0;

        CsvRecordMeta meta = default;

        while (linesRead < slices.Length)
        {
        Seek:
            int index = meta.quoteCount % 2 == 0
                ? data.IndexOfAny(_quote, _newline[0])
                : data.IndexOf(_quote);

            if (index < 0)
                break;

            if (_quote == data.DangerousGetReferenceAt(index))
            {
                meta.quoteCount++;
                data = data.Slice(index + 1);
                currentConsumed += index + 1;
                goto Seek;
            }

            currentConsumed += index;

            // find LF if we have 2 token newline
            if (_newline.Length == 2)
            {
                // ran out of data
                if (index >= data.Length - 1)
                    break;

                // next token wasn't the second newline
                if (_newline[1] != data.DangerousGetReferenceAt(index + 1))
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
