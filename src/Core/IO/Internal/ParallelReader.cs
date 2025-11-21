using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal static class ParallelReader
{
    public static IParallelReader<char> Create(ReadOnlyMemory<char> csv, CsvOptions<char> options)
    {
        return new ParallelMemoryReader<char>(csv, options);
    }

    public static IParallelReader<byte> Create(ReadOnlyMemory<byte> csv, CsvOptions<byte> options)
    {
        return new ParallelMemoryReader<byte>(csv, options);
    }

    public static IParallelReader<byte> Create(Stream stream, CsvOptions<byte> options, CsvIOOptions ioOptions)
    {
        return new ParallelStreamReader(stream, options, ioOptions);
    }

    public static IParallelReader<char> Create(TextReader reader, CsvOptions<char> options, CsvIOOptions ioOptions)
    {
        return new ParallelTextReader(reader, options, ioOptions);
    }
}
