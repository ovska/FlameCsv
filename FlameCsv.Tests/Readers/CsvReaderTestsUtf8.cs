using System.IO.Pipelines;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvOptions<byte> CreateOptions(string newline, char? escape)
    {
        return new CsvUtf8Options
        {
            DateTimeFormat = 'O',
            Newline = newline,
            Escape = escape,
        };
    }

    protected override IDisposable? GetMemory(ArrayPoolBufferWriter<char> writer, out ReadOnlyMemory<byte> memory)
    {
        var owner = GetMemoryOwner(writer);
        memory = owner.Memory;
        return owner;
    }

    protected override IAsyncEnumerable<Obj> GetObjects(Stream stream, CsvOptions<byte> options, int bufferSize)
    {
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
