using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public abstract class CsvReaderTestsBase
{
    private static IEnumerable<(CsvNewline, bool, int, Escaping, bool, bool?)> BaseData =>
        from crlf in (CsvNewline[])[CsvNewline.CRLF, CsvNewline.LF]
        from writeHeader in GlobalData.Booleans
        from bufferSize in _bufferSizes
        from escaping in GlobalData.Enum<Escaping>()
        from sourceGen in GlobalData.Booleans
        from guarded in GlobalData.GuardedMemory
        select (crlf, writeHeader, bufferSize, escaping, sourceGen, guarded);

    protected static readonly int[] _bufferSizes = [-1, 256, 1024];

    public static TheoryData<CsvNewline, bool, int, Escaping, bool, bool?> RecordData => [.. BaseData];

    public static TheoryData<CsvNewline, bool, int, Escaping, bool, bool, bool?> ObjectData =>
        [
            .. from tuple in BaseData
            from parallel in GlobalData.Booleans
            select (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, parallel, tuple.Item6),
        ];
}

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
public abstract class CsvReaderTestsBase<T> : CsvReaderTestsBase
    where T : unmanaged, IBinaryInteger<T>
{
    protected abstract CsvTypeMap<T, Obj> TypeMap { get; }

    protected abstract Csv.IReadBuilder<T> GetBuilder(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize,
        IBufferPool pool
    );

    [Theory, MemberData(nameof(ObjectData))]
    public async Task Objects_Sync(
        CsvNewline newline,
        bool header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool parallel,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        CsvOptions<T> options = GetOptions(newline, header);
        var memory = TestDataGenerator.Generate<T>(newline, header, escaping);

        using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
        {
            IEnumerable<Obj> enumerable;

            if (parallel)
            {
                Assert.Skip("Parallel reading is not supported yet with sequences.");
                return;
            }
            else
            {
                enumerable = sourceGen
                    ? Csv.From(in sequence).Read<Obj>(TypeMap, options)
                    : Csv.From(in sequence).Read<Obj>(options);
            }

            await Validate(SyncAsyncEnumerable.Create(enumerable), escaping, parallel);
        }
    }

    [Theory, MemberData(nameof(RecordData))]
    public async Task Records_Sync(
        CsvNewline newline,
        bool header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header);

            var memory = TestDataGenerator.Generate<T>(newline, header, escaping);
            using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
            {
                var builder = Csv.From(in sequence);

                var items = await GetItems(builder, options, sourceGen, header, newline, isAsync: false);

                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
    }

    [Theory, MemberData(nameof(ObjectData))]
    public async Task Objects_Async(
        CsvNewline newline,
        bool header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool parallel,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        await Validate(Enumerate(), escaping, parallel);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header);

            var data = TestDataGenerator.Generate<byte>(newline, header, escaping);

            await using var stream = data.AsStream();
            var builder = GetBuilder(stream, options, bufferSize, pool);

            IAsyncEnumerable<Obj> source;

            if (parallel)
            {
                source = ParallelCore();

                async IAsyncEnumerable<Obj> ParallelCore()
                {
                    var parallelBuilder = builder.AsParallel(GetParallelOptions());
                    var enumerable = sourceGen
                        ? parallelBuilder.ReadUnorderedAsync<Obj>(TypeMap, options)
                        : parallelBuilder.ReadUnorderedAsync<Obj>(options);

                    await foreach (var segment in enumerable.WithTestContext())
                    {
                        await Task.Yield();

                        foreach (var item in segment)
                        {
                            yield return item;
                        }
                    }
                }
            }
            else
            {
                source = sourceGen
                    ? new CsvTypeMapEnumerable<T, Obj>(builder, options, TypeMap)
                    : new CsvValueEnumerable<T, Obj>(builder, options);
            }

            await foreach (var obj in source.WithTestContext())
            {
                yield return obj;
            }
        }
    }

    [Theory, MemberData(nameof(RecordData))]
    public async Task Records_Async(
        CsvNewline newline,
        bool header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header);

            var data = TestDataGenerator.Generate<byte>(newline, header, escaping);
            await using var stream = data.AsStream();
            var builder = GetBuilder(stream, options, bufferSize, pool);

            var items = await GetItems(builder, options, sourceGen, header, newline, isAsync: true);

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    protected static async Task Validate(IAsyncEnumerable<Obj> enumerable, Escaping escaping, bool parallel = false)
    {
        int i = 0;

        if (parallel)
        {
            List<Obj> list = await SyncAsyncEnumerable.ToListAsync(enumerable);
            list.Sort();
            enumerable = SyncAsyncEnumerable.Create(list);
        }

        await foreach (var obj in enumerable.WithTestContext())
        {
            Assert.Equal(i, obj.Id);
            Assert.Equal(escaping != Escaping.None ? $"Name\"{i}" : $"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator.GuidBytes), obj.Token);

            i++;
        }

        Assert.Equal(1_000, i);
    }

    protected async Task<List<Obj>> GetItems(
        Csv.IReadBuilder<T> builder,
        CsvOptions<T> options,
        bool sourceGen,
        bool hasHeader,
        CsvNewline newline,
        bool isAsync
    )
    {
        int index = 0;
        long tokenPosition = 0;

        int newlineLength = newline.IsCRLF() ? 2 : 1;

        if (hasHeader)
        {
            tokenPosition = TestDataGenerator.Header.Length + newlineLength;
        }

        List<Obj> items = [];

        if (isAsync)
        {
            await foreach (var record in new CsvRecordAsyncEnumerable<T>(builder, options).WithTestContext())
            {
                items.Add(Core(in record));
            }
        }
        else
        {
            foreach (var record in new CsvRecordEnumerable<T>(builder, options))
            {
                items.Add(Core(in record));
            }
        }

        return items;

        Obj Core(in CsvRecord<T> record)
        {
            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            int length = record
                ._slice.Record.GetFields(record._slice.Reader._recordBuffer)
                .GetRecordLength(record._slice.Record.IsFirst, includeTrailingNewline: true);
            tokenPosition += length;

            Obj obj = new()
            {
                Id = record.ParseField<int>(hasHeader ? "Id" : 0),
                Name = record.ParseField<string?>(hasHeader ? "Name" : 1),
                IsEnabled = record.ParseField<bool>(hasHeader ? "IsEnabled" : 2),
                LastLogin = record.ParseField<DateTimeOffset>(hasHeader ? "LastLogin" : 3),
                Token = record.ParseField<Guid>(hasHeader ? "Token" : 4),
            };

            Obj parsed = sourceGen ? record.ParseRecord(TypeMap) : record.ParseRecord<Obj>();
            Assert.Equal(obj, parsed);

            Assert.Equal(5, record.FieldCount);

            return obj;
        }
    }

    protected static CsvOptions<T> GetOptions(CsvNewline newline, bool header)
    {
        return new CsvOptions<T>
        {
            Formats = { [typeof(DateTime)] = "O" },
            Newline = newline,
            HasHeader = header,
        };
    }

    private static CsvParallelOptions GetParallelOptions()
    {
        return new CsvParallelOptions { };
    }
}
