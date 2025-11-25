using System.Text;

namespace FlameCsv.IO.Internal;

internal static class ParallelReader
{
    public static IParallelReader<char> Create(
        ReadOnlyMemory<char> csv,
        CsvOptions<char> options,
        IBufferPool? bufferPool
    )
    {
        return new ParallelMemoryReader<char>(csv, options, bufferPool);
    }

    public static IParallelReader<byte> Create(
        ReadOnlyMemory<byte> csv,
        CsvOptions<byte> options,
        IBufferPool? bufferPool
    )
    {
        return new ParallelMemoryReader<byte>(csv, options, bufferPool);
    }

    public static IParallelReader<byte> Create(Stream stream, CsvOptions<byte> options, CsvIOOptions ioOptions)
    {
        return new ParallelStreamReader(stream, options, ioOptions);
    }

    public static IParallelReader<char> Create(
        Stream stream,
        Encoding? encoding,
        CsvOptions<char> options,
        CsvIOOptions ioOptions
    )
    {
        StreamReader reader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            ioOptions.BufferSize,
            leaveOpen: false
        );
        return new ParallelTextReader(reader, options, ioOptions);
    }

    public static IParallelReader<char> Create(TextReader reader, CsvOptions<char> options, CsvIOOptions ioOptions)
    {
        return new ParallelTextReader(reader, options, ioOptions);
    }
}
