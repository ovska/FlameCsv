using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Parallel;
using FlameCsv.Reading;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsText : CsvReaderTestsBase<char>
{
    protected override CsvTypeMap<char, Obj> TypeMap => ObjCharTypeMap.Default;

    protected override IAsyncEnumerable<CsvValueRecord<char>> GetRecords(
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
    public void Should_Read_Long_Multisegment_Lines()
    {
        string name = new('x', 512);
        string data = $"0,{name},true,{DateTime.UnixEpoch:o},{Guid.Empty}{Environment.NewLine}";

        List<Obj> objs = [..CsvReader.Read<Obj>(data, new CsvOptions<char> { HasHeader = false })];

        Assert.Single(objs);
        var obj = objs[0];
        Assert.Equal(0, obj.Id);
        Assert.Equal(name, obj.Name);
        Assert.True(obj.IsEnabled);
        Assert.Equal(DateTime.UnixEpoch, obj.LastLogin);
        Assert.Equal(Guid.Empty, obj.Token);
    }

    readonly struct Selector() : ICsvParallelTryInvoke<char, Obj>
    {
        private readonly StrongBox<IMaterializer<char, Obj>> _materializer = new();

        public bool TryInvoke<TReader>(scoped ref TReader reader, in CsvParallelState state, [NotNullWhen(true)] out Obj? result)
            where TReader : ICsvRecordFields<char>, allows ref struct
        {
            if (_materializer.Value is null)
            {
                _materializer.Value = ObjCharTypeMap.Default.GetMaterializer(
                    ["Id", "Name", "IsEnabled", "LastLogin", "Token"],
                    CsvOptions<char>.Default);

                result = null;
                return false;
            }

            result = _materializer.Value.Parse(ref reader);
            return true;
        }
    }
}
