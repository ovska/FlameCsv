using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO;
using FlameCsv.Utilities;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserRFC4180<T>(
    CsvOptions<T> options,
    ICsvPipeReader<T> reader,
    in CsvParserOptions<T> parserOptions)
    : CsvParser<T>(options, reader, in parserOptions)
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
        SearchValues<T> nextToken = _dialect.GetFindToken();
        NewlineBuffer<T> newline = _dialect.Newline;

        while (!reader.End)
        {
        Seek:
            int index = quoteCount % 2 == 0
                ? reader.UnreadSpan.IndexOfAny(nextToken)
                : reader.UnreadSpan.IndexOf(quote);

            if (index != -1)
            {
                T match = reader.UnreadSpan[index];

                if (match == quote)
                {
                    reader.Advance(index + 1);

                    // quotes are commonly followed by delimiters, newlines or other quotes
                    if ((++quoteCount & 1) == 0)
                    {
                        if (reader.TryPeek(out match))
                        {
                            if (match == quote)
                            {
                                quoteCount++;
                                reader.Advance(1);
                                goto Seek;
                            }

                            if (match == delimiter)
                            {
                                reader.Advance(1);
                                goto FoundDelimiter;
                            }

                            if (match == newline.First || match == newline.Second)
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

                if (match == delimiter)
                {
                    reader.Advance(index + 1);
                    goto FoundDelimiter;
                }

                Debug.Assert(newline.Length != 0);
                Debug.Assert(match == newline.First || match == newline.Second);

                // must be newline token
                reader.Advance(index);
                goto FoundNewline;

            FoundDelimiter:
                fieldMeta.Append(Meta.RFC((int)reader.Consumed - 1, quoteCount, isEOL: false, newline.Length));
                quoteCount = 0;
                goto Seek;

            FoundNewline:
                // store first newline token position
                var crPosition = reader.Position;

                // ...and advance past it
                reader.Advance(1);

                // at this point, the record has ended no matter if we find another newline token or not
                bool twoTokens =
                    newline.Length != 1 &&
                    match == newline.First &&
                    reader.IsNext(newline.Second, advancePast: true);

                int newlineLength = twoTokens ? 2 : 1;

                fieldMeta.Append(
                    Meta.RFC(
                        end: (int)reader.Consumed - newlineLength,
                        quoteCount: quoteCount,
                        isEOL: true,
                        newlineLength: newlineLength));

                fields = new CsvFields<T>(
                    this,
                    _multisegmentAllocator.AsMemory(reader.Sequence.Slice(reader.Sequence.Start, crPosition)),
                    GetSegmentMeta(fieldMeta.AsSpan()));

                _sequence = reader.UnreadSequence;
                _previousEndCR = newline.Length == 2 && match == newline.First && !twoTokens && _sequence.IsEmpty;
                return true;
            }

            // nothing in this segment
            reader.Advance(reader.UnreadSpan.Length);
        }

        if (isFinalBlock && !_sequence.IsEmpty)
        {
            var lastLine = _multisegmentAllocator.AsMemory(_sequence);
            _sequence = default;

            // check if the last line was only whitespace
            if (_dialect.Trimming is CsvFieldTrimming.None || lastLine.Span.ContainsAnyExcept(T.CreateTruncating(' ')))
            {
                // the last field ended in a delimiter, so there must be at least one field after it
                // this should be an EOL with newline length 0
                fieldMeta.Append(Meta.RFC(lastLine.Length, quoteCount, isEOL: true, newlineLength: 0));

                fields = new CsvFields<T>(this, lastLine, GetSegmentMeta(fieldMeta.AsSpan()));
                return true;
            }
        }

        Unsafe.SkipInit(out fields);
        return false;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected override int ReadFromSpan(ReadOnlySpan<T> data)
    {
        // the prefetch reads one vector ahead
        const int minimumVectors = 2;

        if (Unsafe.SizeOf<T>() == sizeof(char) && (Vec512Char.IsSupported || Vec256Char.IsSupported || Vec128Char.IsSupported))
        {
            ReadOnlySpan<char> dataT = MemoryMarshal.Cast<T, char>(data);
            ref readonly var dialect = ref Unsafe.As<CsvDialect<T>, CsvDialect<char>>(ref Unsafe.AsRef(in _dialect));
            var newline = dialect._newline;

            if (Vec512Char.IsSupported && dataT.Length > Vec512Char.Count * minimumVectors)
            {
                if (dialect._newline.Length == 1)
                {
                    return FieldParser<char, NewlineParserOne<char, Vec512Char>, Vec512Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(dialect._newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<char, NewlineParserTwo<char, Vec512Char>, Vec512Char>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(dialect._newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

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
        }

        if (Unsafe.SizeOf<T>() == sizeof(byte) && (Vec512Byte.IsSupported || Vec256Byte.IsSupported || Vec128Byte.IsSupported))
        {
            ReadOnlySpan<byte> dataT = MemoryMarshal.Cast<T, byte>(data);
            ref readonly var dialect = ref Unsafe.As<CsvDialect<T>, CsvDialect<byte>>(ref Unsafe.AsRef(in _dialect));
            var newlineT = _dialect.Newline;
            var newline = Unsafe.As<NewlineBuffer<T>, NewlineBuffer<byte>>(ref newlineT);

            if (Vec512Byte.IsSupported && dataT.Length > Vec512Byte.Count * minimumVectors)
            {
                if (newline.Length == 1)
                {
                    return FieldParser<byte, NewlineParserOne<byte, Vec512Byte>, Vec512Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First),
                        dataT,
                        MetaBuffer);
                }

                if (newline.Length == 2)
                {
                    return FieldParser<byte, NewlineParserTwo<byte, Vec512Byte>, Vec512Byte>.Core(
                        dialect.Delimiter,
                        dialect.Quote,
                        new(newline.First, newline.Second),
                        dataT,
                        MetaBuffer);
                }
            }

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
        }

        // TODO: fix SequentialParser to support partial newlines

        return 0;
    }
}
