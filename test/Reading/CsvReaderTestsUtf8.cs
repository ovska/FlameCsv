using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvTypeMap<byte, Obj> TypeMap => ObjByteTypeMap.Default;

    protected override ICsvBufferReader<byte> GetReader(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize,
        IBufferPool pool
    )
    {
        return CsvBufferReader.Create(
            stream,
            new()
            {
                BufferSize = bufferSize,
                MinimumReadSize = bufferSize == -1 ? -1 : bufferSize / 2,
                BufferPool = pool,
            }
        );
    }
}
