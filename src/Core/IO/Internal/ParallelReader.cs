using System.Buffers;
using System.Text;

namespace FlameCsv.IO.Internal;

internal static class ParallelReader
{
    public static IParallelReader<T> Create<T>(ReadOnlyMemory<T> csv, CsvOptions<T> options, in CsvIOOptions ioOptions)
        where T : unmanaged, IBinaryInteger<T>
    {
        return new ParallelSequenceReader<T>(new ReadOnlySequence<T>(csv), options, in ioOptions);
    }

    public static IParallelReader<T> Create<T>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options,
        in CsvIOOptions ioOptions
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        return new ParallelSequenceReader<T>(in csv, options, in ioOptions);
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
