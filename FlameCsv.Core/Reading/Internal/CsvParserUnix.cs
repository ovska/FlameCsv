using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Utilities;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserUnix<T>(CsvOptions<T> options, ICsvPipeReader<T> reader)
    : CsvParser<T>(options, reader)
    where T : unmanaged, IBinaryInteger<T>
{
    private protected override bool TryReadFromSequence(out CsvFields<T> fields, bool isFinalBlock)
    {
        if (!_dialect.Escape.HasValue)
        {
            Throw.Unreachable("Escape character not set.");
        }

        using ValueListBuilder<Meta> fieldMeta = new(stackalloc Meta[16]);

        // load into locals for faster access
        T delimiter = _dialect.Delimiter;
        T quote = _dialect.Quote;
        T escape = _dialect.Escape.Value;
        SearchValues<T> nextToken = _dialect.GetFindToken(_newline.Length);
        NewlineBuffer<T> newline = _newline;

        uint quoteCount = 0;
        uint escapeCount = 0;
        SequenceReader<T> reader = new(_sequence);

        while (!reader.End)
        {
        Seek:
            int index = quoteCount % 2 == 0
                ? reader.UnreadSpan.IndexOfAny(nextToken)
                : reader.UnreadSpan.IndexOfAny(quote, escape);

            if (index != -1)
            {
                if (reader.UnreadSpan[index] == escape)
                {
                    escapeCount++;

                    reader.Advance(index + 1);

                    // escape was the last token?
                    if (reader.End)
                    {
                        break;
                    }

                    // skip past the escaped token
                    reader.Advance(1);
                    goto Seek;
                }

                if (reader.UnreadSpan[index] == quote)
                {
                    reader.Advance(index + 1);

                    // quotes are commonly followed by delimiters, newlines or other quotes
                    if ((++quoteCount & 1) == 0)
                    {
                        if (reader.TryPeek(out T next))
                        {
                            if (next == delimiter)
                            {
                                reader.Advance(1);
                                goto FoundDelimiter;
                            }

                            if (next == newline.First)
                            {
                                // don't advance here so the first position of a CRLF is preserved
                                goto FoundNewline;
                            }
                        }
                        else
                        {
                            // end of the sequence
                            break;
                        }
                    }

                    goto Seek;
                }

                if (reader.UnreadSpan[index] == delimiter)
                {
                    reader.Advance(index + 1);
                    goto FoundDelimiter;
                }

                Debug.Assert(newline.Length != 0);
                Debug.Assert(reader.UnreadSpan[index] == newline.First);

                // must be newline token
                reader.Advance(index);
                goto FoundNewline;

            FoundDelimiter:
                fieldMeta.Append(Meta.Unix((int)reader.Consumed - 1, quoteCount, escapeCount, isEOL: false));
                quoteCount = 0;
                escapeCount = 0;
                goto Seek;

            FoundNewline:
                // store first newline token position
                var crPosition = reader.Position;

                // ...and advance past it
                reader.Advance(1);

                if (newline.Length == 1 || reader.IsNext(newline.Second, advancePast: true))
                {
                    fieldMeta.Append(
                        Meta.Unix((int)reader.Consumed - newline.Length, quoteCount, escapeCount, isEOL: true));

                    fields = new CsvFields<T>(
                        this,
                        reader
                            .Sequence.Slice(reader.Sequence.Start, crPosition)
                            .AsMemory(Options._memoryPool, ref _multisegmentBuffer),
                        GetSegmentMeta(fieldMeta.AsSpan()));

                    _sequence = reader.UnreadSequence;
                    return true;
                }

                if (reader.End)
                    break;

                reader.Advance(1);
                goto Seek;
            }

            // nothing in this segment
            reader.Advance(reader.UnreadSpan.Length);
        }

        if (isFinalBlock && !_sequence.IsEmpty)
        {
            var lastLine = _sequence.AsMemory(Options._memoryPool, ref _multisegmentBuffer);
            _sequence = default;

            // the remaining data is either after a delimiter if fields is non-empty, or
            // some trailing data after the last newline.
            // this should _not_ be an EOL as that flag is used for determining record length w/ the newline
            fieldMeta.Append(Meta.Unix(lastLine.Length, quoteCount, escapeCount, isEOL: false));

            fields = new CsvFields<T>(this, lastLine, GetSegmentMeta(fieldMeta.AsSpan()));
            return true;
        }

        Unsafe.SkipInit(out fields);
        return false;
    }

    private protected override int ReadFromFirstSpan()
    {
        // TODO: implement
        return 0;
    }
}
