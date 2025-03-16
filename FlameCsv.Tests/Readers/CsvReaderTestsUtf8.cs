using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvTypeMap<byte, Obj> TypeMap => ObjByteTypeMap.Default;

    protected override ICsvPipeReader<byte> GetReader(Stream stream, CsvOptions<byte> options, int bufferSize)
    {
        return CsvPipeReader.Create(stream, options.Allocator, new() { BufferSize = bufferSize });
    }
}
