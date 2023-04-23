using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvReaderOptions<byte> CreateOptions(string newline, char? escape)
    {
        return new CsvUtf8ReaderOptions
        {
            DateTimeFormat = 'O',
            Newline = Encoding.UTF8.GetBytes(newline),
            Escape = escape.HasValue ? (byte)escape.Value : null,
        };
    }

    protected override IDisposable? GetMemory(ArrayPoolBufferWriter<char> writer, out ReadOnlyMemory<byte> memory)
    {
        var owner = GetMemoryOwner(writer);
        memory = owner.Memory;
        return owner;
    }

    protected override IAsyncEnumerable<Obj> GetObjects(Stream stream, CsvReaderOptions<byte> options, int bufferSize)
    {
        return CsvReader.ReadAsync<Obj>(
            PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize)),
            options);
    }

    protected override CsvRecordAsyncEnumerable<byte> GetRecords(
        Stream stream,
        CsvReaderOptions<byte> options,
        int bufferSize)
    {
        return CsvReader.EnumerateAsync(
            PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: bufferSize)),
            options);
    }
}
