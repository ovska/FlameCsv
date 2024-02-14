using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvTypeMap<byte, Obj> TypeMap => ObjByteTypeMap.Instance;

    protected override CsvOptions<byte> CreateOptions(string newline, char? escape)
    {
        return new CsvUtf8Options
        {
            DateTimeFormat = 'O',
            Newline = newline,
            Escape = escape,
        };
    }

    protected override ReadOnlyMemory<byte> GetMemory(ReadOnlyMemory<char> text)
    {
        return Encoding.UTF8.GetBytes(new ReadOnlySequence<char>(text));
    }

    protected override IAsyncEnumerable<Obj> GetObjects(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize,
        bool sourceGen)
    {
        if (sourceGen)
        {
            return CsvReader.ReadAsync(
                PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize)),
                TypeMap,
                options);
        }

        return CsvReader.ReadAsync<Obj>(
            PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize)),
            options);
    }

    protected override CsvRecordAsyncEnumerable<byte> GetRecords(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize)
    {
        return CsvReader.EnumerateAsync(
            PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize)),
            options);
    }
}
