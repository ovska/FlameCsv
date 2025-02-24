using System.Text;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public sealed class CsvReaderTestsText : CsvReaderTestsBase<char>
{
    protected override CsvTypeMap<char, Obj> TypeMap => ObjCharTypeMap.Default;

    protected override CsvRecordEnumerable<char> GetRecords(
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

#if FEATURE_PARALLEL
    [Theory, InlineData(false), InlineData(true)]
    public async Task Should_Read_Parallel(bool isAsync)
    {
        const NewlineToken newline = NewlineToken.LF;
        const bool header = true;
        const bool trailingLF = true;
        const Mode escaping = Mode.RFC;

        using var pool = ReturnTrackingMemoryPool<char>.Create();

        var data = TestDataGenerator.Generate<char>(newline, header, trailingLF, escaping);
        var options = GetOptions(newline, header, escaping, pool);

        List<Obj> objs;

        if (isAsync)
        {
            var bag = new ConcurrentBag<Obj>();

            await Parallel.ForEachAsync(
                CsvParallel.Test<char, Obj>(new ReadOnlySequence<char>(data), options),
                CancellationToken.None,
                (obj, _) =>
                {
                    bag.Add(obj);
                    return default;
                });

            objs = bag.ToList();
        }
        else
        {
            ParallelQuery<Obj> query = CsvParallel
                .Read<char, Obj>(data, options)
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .WithMergeOptions(ParallelMergeOptions.NotBuffered);

            objs = [..query];
        }

        objs.Sort((a, b) => a.Id.CompareTo(b.Id));

        await Validate(new SyncAsyncEnumerable<Obj>(objs), escaping);
    }
#endif
}
