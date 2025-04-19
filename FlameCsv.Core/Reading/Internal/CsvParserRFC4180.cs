using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO;

namespace FlameCsv.Reading.Internal;

internal sealed partial class CsvParserRFC4180<T>(
    CsvOptions<T> options,
    ICsvBufferReader<T> reader,
    in CsvParserOptions<T> parserOptions)
    : CsvParser<T>(options, reader, in parserOptions)
    where T : unmanaged, IBinaryInteger<T>
{
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected override int ParseFromBuffer()
    {
        // the prefetch reads one vector ahead
        const int minimumVectors = 2;

        ReadOnlySpan<T> data = Buffer;

        if (Unsafe.SizeOf<T>() is sizeof(char) &&
            (Vec512Char.IsSupported || Vec256Char.IsSupported || Vec128Char.IsSupported))
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

        if (Unsafe.SizeOf<T>() is sizeof(byte) &&
            (Vec512Byte.IsSupported || Vec256Byte.IsSupported || Vec128Byte.IsSupported))
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

        // if we are here, we either have only the tail of the data remaining, or SIMD is not supported
        if (IsReaderCompleted || !SimdVector.SupportsAny)
        {
            if (_dialect._newline.Length == 1)
            {
                // single token newlines can process the whole data
                return ParseScalar(new NewlineParserOne<T, NoOpVector<T>>(_dialect._newline.First));
            }

            return ParseScalar(
                new NewlineParserTwo<T, NoOpVector<T>>(_dialect._newline.First, _dialect._newline.Second));
        }

        return 0;
    }
}
