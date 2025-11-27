using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public sealed class CsvReaderTestsUtf8 : CsvReaderTestsBase<byte>
{
    protected override CsvTypeMap<byte, Obj> TypeMap => ObjByteTypeMap.Default;

    protected override Csv.IReadBuilder<byte> GetBuilder(
        Stream stream,
        CsvOptions<byte> options,
        int bufferSize,
        IBufferPool pool
    )
    {
        return Csv.From(
            stream,
            new()
            {
                BufferPool = pool,
                BufferSize = bufferSize,
                MinimumReadSize = bufferSize == -1 ? -1 : bufferSize / 2,
            }
        );
    }
}
