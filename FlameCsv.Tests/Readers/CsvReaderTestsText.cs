using System.Text;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsText : CsvReaderTestsBase<char>
{
    protected override CsvTypeMap<char, Obj> TypeMap => ObjCharTypeMap.Instance;

    protected override CsvOptions<char> CreateOptions(NewlineToken newline, char? escape)
    {
        return new CsvOptions<char>
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

    protected override CsvRecordAsyncEnumerable<char> GetRecords(
        Stream stream,
        CsvOptions<char> options,
        int bufferSize)
    {
        return CsvReader.EnumerateAsync(
            new StreamReader(stream, Encoding.UTF8, bufferSize: bufferSize),
            options);
    }

    protected override IAsyncEnumerable<Obj> GetObjects(
        Stream stream,
        CsvOptions<char> options,
        int bufferSize,
        bool sourceGen)
    {
        if (sourceGen)
        {
            return CsvReader.ReadAsync(
                new StreamReader(stream, Encoding.UTF8, bufferSize: bufferSize),
                TypeMap,
                options);
        }

        return CsvReader.ReadAsync<Obj>(
            new StreamReader(stream, Encoding.UTF8, bufferSize: bufferSize),
            options);
    }

    [Fact]
    public async Task Should_Read_Long_Multisegment_Lines()
    {
        string name = new('x', 1024);
        string data = $"0,{name},true,{DateTime.UnixEpoch:o},{Guid.Empty}{Environment.NewLine}";

        var objs = new List<Obj>();

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
        using var reader = new StreamReader(ms, bufferSize: 128);
        var options = new CsvOptions<char> { HasHeader = false };

        await foreach (var item in CsvReader.ReadAsync<Obj>(reader, options))
        {
            objs.Add(item);
        }

        Assert.Single(objs);
        var obj = objs[0];
        Assert.Equal(0, obj.Id);
        Assert.Equal(name, obj.Name);
        Assert.True(obj.IsEnabled);
        Assert.Equal(DateTime.UnixEpoch, obj.LastLogin);
        Assert.Equal(Guid.Empty, obj.Token);
    }
}
