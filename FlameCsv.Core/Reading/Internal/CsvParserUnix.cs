using FlameCsv.IO;

namespace FlameCsv.Reading.Internal;

internal sealed class CsvParserUnix<T>(
    CsvOptions<T> options,
    ICsvBufferReader<T> reader,
    in CsvParserOptions<T> parserOptions)
    : CsvParser<T>(options, reader, in parserOptions)
    where T : unmanaged, IBinaryInteger<T>
{
    private protected override int ParseFromBuffer()
    {
        throw new NotImplementedException();
    }
}
