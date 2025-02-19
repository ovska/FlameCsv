using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserRFC4180<T>(CsvOptions<T> options, ICsvPipeReader<T> reader)
    : CsvParser<T>(options, reader)
    where T : unmanaged, IBinaryInteger<T>
{
    private protected override bool TryReadFromSequence(out CsvFields<T> fields, bool isFinalBlock)
    {
        Debug.Assert(_dialect.Escape is null);

        using ValueListBuilder<Meta> fieldMeta = new(stackalloc Meta[16]);

        uint quoteCount = 0;
        SequenceReader<T> reader = new(_sequence);

        // load into locals for faster access
        T delimiter = _dialect.Delimiter;
        T quote = _dialect.Quote;
        SearchValues<T> nextToken = _dialect.GetFindToken(_newline.Length);
        NewlineBuffer<T> newline = _newline;

        while (!reader.End)
        {
        Seek:
            int index = quoteCount % 2 == 0
                ? reader.UnreadSpan.IndexOfAny(nextToken)
                : reader.UnreadSpan.IndexOf(quote);

            if (index != -1)
            {
                if (reader.UnreadSpan[index] == quote)
                {
                    reader.Advance(index + 1);

                    // quotes are commonly followed by delimiters, newlines or other quotes
                    if ((++quoteCount & 1) == 0)
                    {
                        if (reader.TryPeek(out T next))
                        {
                            if (next == quote)
                            {
                                quoteCount++;
                                reader.Advance(1);
                                goto Seek;
                            }

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
                fieldMeta.Append(Meta.RFC((int)reader.Consumed - 1, quoteCount, isEOL: false));
                quoteCount = 0;
                goto Seek;

            FoundNewline:
                // store first newline token position
                var crPosition = reader.Position;

                // ...and advance past it
                reader.Advance(1);

                if (newline.Length == 1 || reader.IsNext(newline.Second, advancePast: true))
                {
                    fieldMeta.Append(Meta.RFC((int)reader.Consumed - newline.Length, quoteCount, isEOL: true));

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

                goto Seek;
            }

            // nothing in this segment
            reader.Advance(reader.UnreadSpan.Length);
        }

        if (isFinalBlock && !_sequence.IsEmpty)
        {
            var lastLine = _sequence.AsMemory(Options._memoryPool, ref _multisegmentBuffer);
            _sequence = default;

            // the last field ended in a delimiter, so there must be at least one field after it
            // this should _not_ be an EOL as that flag is used for determining record length w/ the newline
            fieldMeta.Append(Meta.RFC(lastLine.Length, quoteCount, isEOL: false));

            fields = new CsvFields<T>(this, lastLine, GetSegmentMeta(fieldMeta.AsSpan()));
            return true;
        }

        Unsafe.SkipInit(out fields);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected override int ReadFromFirstSpan()
    {
        Debug.Assert(_newline.Length != 0);

        const int minimumVectors = 1; // TODO: fine-tune

        ReadOnlySpan<T> data = _sequence.FirstSpan;

        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            ReadOnlySpan<char> dataT = MemoryMarshal.Cast<T, char>(data);
            ref readonly var dialect = ref Unsafe.As<CsvDialect<T>, CsvDialect<char>>(ref Unsafe.AsRef(in _dialect));
            var newline = Unsafe.As<NewlineBuffer<T>, NewlineBuffer<char>>(ref _newline);

            if (Vec256Char.IsSupported && dataT.Length > Vec256Char.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<char, NewlineParserOne<char, Vec256Char>, Vec256Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<char, NewlineParserTwo<char, Vec256Char>, Vec256Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

            if (Vec128Char.IsSupported && dataT.Length > Vec128Char.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<char, NewlineParserOne<char, Vec128Char>, Vec128Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<char, NewlineParserTwo<char, Vec128Char>, Vec128Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

            if (Vec64Char.IsSupported && dataT.Length > Vec64Char.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<char, NewlineParserOne<char, Vec64Char>, Vec64Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<char, NewlineParserTwo<char, Vec64Char>, Vec64Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }
        }

        if (Unsafe.SizeOf<T>() == sizeof(byte))
        {
            ReadOnlySpan<byte> dataT = MemoryMarshal.Cast<T, byte>(data);
            ref readonly var dialect = ref Unsafe.As<CsvDialect<T>, CsvDialect<byte>>(ref Unsafe.AsRef(in _dialect));
            var newline = Unsafe.As<NewlineBuffer<T>, NewlineBuffer<byte>>(ref _newline);

            if (Vec256Byte.IsSupported && dataT.Length > Vec256Byte.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<byte, NewlineParserOne<byte, Vec256Byte>, Vec256Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<byte, NewlineParserTwo<byte, Vec256Byte>, Vec256Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

            if (Vec128Byte.IsSupported && dataT.Length > Vec128Byte.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<byte, NewlineParserOne<byte, Vec128Byte>, Vec128Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<byte, NewlineParserTwo<byte, Vec128Byte>, Vec128Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

            if (Vec64Byte.IsSupported && dataT.Length > Vec64Byte.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<byte, NewlineParserOne<byte, Vec64Byte>, Vec64Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<byte, NewlineParserTwo<byte, Vec64Byte>, Vec64Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }
        }

        if (SequentialParser<T>.CanRead(data.Length))
        {
            return SequentialParser<T>.Core(in _dialect, _newline, data, MetaBuffer);
        }

        return 0;
    }
}
