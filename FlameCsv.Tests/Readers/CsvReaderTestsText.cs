using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsText : CsvReaderTestsBase<char>
{
    protected override CsvReaderOptions<char> CreateOptions(string newline, char? escape)
    {
        return new CsvTextReaderOptions
        {
            DateTimeFormat = "O",
            Newline = newline,
            Escape = escape,
        };
    }

    protected override IDisposable? GetMemory(ArrayPoolBufferWriter<char> writer, out ReadOnlyMemory<char> memory)
    {
        memory = writer.WrittenMemory;
        return null;
    }

    protected override CsvRecordAsyncEnumerable<char> GetRecords(
        Stream stream,
        CsvReaderOptions<char> options,
        int bufferSize)
    {
        return CsvReader.EnumerateAsync(
            new StreamReader(stream, Encoding.UTF8, bufferSize: bufferSize),
            options);
    }

    protected override IAsyncEnumerable<Obj> GetObjects(Stream stream, CsvReaderOptions<char> options, int bufferSize)
    {
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
        var options = CsvTextReaderOptions.Default;

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
