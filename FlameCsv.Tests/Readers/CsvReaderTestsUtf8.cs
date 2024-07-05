using System.IO.Pipelines;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvTypeMap<byte, Obj> TypeMap => ObjByteTypeMap.Instance;

    protected override CsvOptions<byte> CreateOptions(NewlineToken newline, char? escape)
    {
        return new CsvOptions<byte>
        {
            Formats = { { typeof(DateTime), "O" } },
            Escape = escape,
            Newline = newline switch
            {
                NewlineToken.LF => "\n",
                NewlineToken.CRLF => "\r\n",
                _ => default,
            },
        };
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
