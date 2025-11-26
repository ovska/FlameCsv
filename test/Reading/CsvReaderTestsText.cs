using System.Text;
using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public sealed class CsvReaderTestsText : CsvReaderTestsBase<char>
{
    protected override CsvTypeMap<char, Obj> TypeMap => ObjCharTypeMap.Default;

    protected override ICsvBufferReader<char> GetReader(
        Stream stream,
        CsvOptions<char> options,
        int bufferSize,
        IBufferPool pool
    )
    {
        return CsvBufferReader.Create(
            new StreamReader(stream, Encoding.UTF8, bufferSize: bufferSize),
            new CsvIOOptions
            {
                BufferSize = bufferSize,
                MinimumReadSize = bufferSize == -1 ? -1 : bufferSize / 2,
                BufferPool = pool,
            }
        );
    }

    [Fact]
    public void Should_Read_Long_Multisegment_Lines()
    {
        string name = new('x', 512);
        string data = $"0,{name},true,{DateTime.UnixEpoch:o},{Guid.Empty}{Environment.NewLine}";

        List<Obj> objs = [.. Csv.From(data).Read<Obj>(new CsvOptions<char> { HasHeader = false })];

        Assert.Single(objs);
        var obj = objs[0];
        Assert.Equal(0, obj.Id);
        Assert.Equal(name, obj.Name);
        Assert.True(obj.IsEnabled);
        Assert.Equal(DateTime.UnixEpoch, obj.LastLogin);
        Assert.Equal(Guid.Empty, obj.Token);
    }
}
