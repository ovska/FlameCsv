using System.Buffers;
using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public abstract class CsvReaderTestsBase
{
    protected static readonly int[] _bufferSizes = [-1, 256, 1024];

    public sealed class SyncData : TheoryData<CsvNewline, bool, bool, int, Escaping, bool, bool, bool?>;

    public sealed class AsyncData : TheoryData<CsvNewline, bool, bool, int, Escaping, bool, bool, bool?>;

    public static SyncData SyncParams
    {
        get
        {
            var data =
                from crlf in (CsvNewline[])[CsvNewline.CRLF, CsvNewline.LF]
                from writeHeader in GlobalData.Booleans
                from writeTrailingNewline in (bool[])[true] // GlobalData.Booleans
                from bufferSize in _bufferSizes
                from escaping in GlobalData.Enum<Escaping>()
                from parallel in (bool[])[false] //GlobalData.Booleans
                from sourceGen in GlobalData.Booleans
                from guarded in GlobalData.GuardedMemory
                select (crlf, writeHeader, writeTrailingNewline, bufferSize, escaping, parallel, sourceGen, guarded);
            return [.. data];
        }
    }

    public static AsyncData AsyncParams
    {
        get
        {
            var data =
                from crlf in GlobalData.Enum<CsvNewline>()
                from writeHeader in GlobalData.Booleans
                from writeTrailingNewline in (bool[])[true] // GlobalData.Booleans
                from bufferSize in _bufferSizes
                from escaping in GlobalData.Enum<Escaping>()
                from parallel in (bool[])[false] // GlobalData.Booleans
                from sourceGen in GlobalData.Booleans
                from guarded in GlobalData.GuardedMemory
                select (crlf, writeHeader, writeTrailingNewline, bufferSize, escaping, parallel, sourceGen, guarded);

            return [.. data];
        }
    }
}

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
public abstract class CsvReaderTestsBase<T> : CsvReaderTestsBase
    where T : unmanaged, IBinaryInteger<T>
{
    protected abstract CsvTypeMap<T, Obj> TypeMap { get; }

    protected abstract ICsvBufferReader<T> GetReader(Stream stream, CsvOptions<T> options, int bufferSize);

    [Theory, MemberData(nameof(SyncParams))]
    public async Task Objects_Sync(
        CsvNewline newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Escaping escaping,
        bool parallel,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = ReturnTrackingMemoryPool<T>.Create(guarded);
        CsvOptions<T> options = GetOptions(newline, header, pool);
        var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);

        using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
        {
            IEnumerable<Obj> enumerable = sourceGen
                ? new CsvTypeMapEnumerable<T, Obj>(in sequence, options, TypeMap)
                : new CsvValueEnumerable<T, Obj>(in sequence, options);

            if (parallel)
                enumerable = WrapParallel(enumerable);

            await Validate(SyncAsyncEnumerable.Create(enumerable), escaping);
        }
    }

    [Theory, MemberData(nameof(SyncParams))]
    public async Task Records_Sync(
        CsvNewline newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Escaping escaping,
        bool parallel,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = ReturnTrackingMemoryPool<T>.Create(guarded);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, pool);

            var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
            using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
            {
                await using var reader = CsvBufferReader.Create(in sequence);

                var items = await GetItems(reader, options, sourceGen, header, newline, parallel, isAsync: false);

                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
    }

    [Theory, MemberData(nameof(AsyncParams))]
    public async Task Objects_Async(
        CsvNewline newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Escaping escaping,
        bool parallel,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = ReturnTrackingMemoryPool<T>.Create(guarded);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, pool);

            var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);

            await using var stream = data.AsStream();
            await using var reader = GetReader(stream, options, bufferSize);

            IAsyncEnumerable<Obj> source = sourceGen
                ? new CsvTypeMapEnumerable<T, Obj>(reader, options, TypeMap)
                : new CsvValueEnumerable<T, Obj>(reader, options);

            if (parallel)
                source = WrapParallelAsync(source);

            await foreach (var obj in source.WithTestContext())
            {
                yield return obj;
            }
        }
    }

    [Theory, MemberData(nameof(AsyncParams))]
    public async Task Records_Async(
        CsvNewline newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Escaping escaping,
        bool parallel,
        bool sourceGen,
        bool? guarded
    )
    {
        using var pool = ReturnTrackingMemoryPool<T>.Create(guarded);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, pool);

            var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);
            await using var stream = data.AsStream();
            await using var reader = GetReader(stream, options, bufferSize);

            var items = await GetItems(reader, options, sourceGen, header, newline, parallel, isAsync: true);

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    protected static async Task Validate(IAsyncEnumerable<Obj> enumerable, Escaping escaping)
    {
        int i = 0;

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
        ICsvBufferReader<T> reader,
        CsvOptions<T> options,
        bool sourceGen,
        bool hasHeader,
        CsvNewline newline,
        bool parallel,
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

        if (parallel)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            if (isAsync)
            {
                await CsvParallel.ForEachAsync(
                    reader,
                    (record, _) =>
                    {
                        var obj = Core(record);
                        lock (items)
                            items.Add(obj);
                        return default;
                    },
                    options,
                    new ParallelOptions { CancellationToken = cts.Token }
                );
            }
            else
            {
                CsvParallel.ForEach(
                    reader,
                    record =>
                    {
                        var obj = Core(record);
                        lock (items)
                            items.Add(obj);
                    },
                    options,
                    new ParallelOptions { CancellationToken = cts.Token }
                );
            }

            items.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        else
        {
            var enumerable = new CsvRecordEnumerable<T>(reader, options);

            if (isAsync)
            {
                await foreach (var record in enumerable.WithTestContext())
                {
                    items.Add(Core(in record));
                }
            }
            else
            {
                foreach (var record in enumerable)
                {
                    items.Add(Core(in record));
                }
            }
        }

        return items;

        Obj Core(in CsvRecord<T> record)
        {
            if (!parallel)
            {
                index++;
                Assert.Equal(hasHeader ? index + 1 : index, record.Line);
                Assert.Equal(tokenPosition, record.Position);

                int length = record
                    ._slice.Record.GetFields(record._slice.Reader._recordBuffer)
                    .GetRecordLength(record._slice.Record.IsFirst, includeTrailingNewline: true);
                tokenPosition += length;
            }

            Obj obj = new()
            {
                Id = record.ParseField<int>(0),
                Name = record.ParseField<string?>(1),
                IsEnabled = record.ParseField<bool>(2),
                LastLogin = record.ParseField<DateTimeOffset>(3),
                Token = record.ParseField<Guid>(4),
            };

            Obj parsed = sourceGen ? record.ParseRecord(TypeMap) : record.ParseRecord<Obj>();
            Assert.Equal(obj, parsed);

            Assert.Equal(5, record.FieldCount);

            return obj;
        }
    }

    protected static CsvOptions<T> GetOptions(CsvNewline newline, bool header, MemoryPool<T> pool)
    {
        return new CsvOptions<T>
        {
            Formats = { [typeof(DateTime)] = "O" },
            Newline = newline,
            HasHeader = header,
            MemoryPool = pool,
#if false
            ExceptionHandler = static (in CsvExceptionHandlerArgs<T> args) =>
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var str = args.Options.GetAsString(args.Record.Span);
                }

                return false;
            },
#endif
        };
    }

    protected static IEnumerable<Obj> WrapParallel(IEnumerable<Obj> enumerable) =>
        enumerable
            .AsParallel()
            .AsOrdered()
            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .WithCancellation(TestContext.Current.CancellationToken);

    protected static async IAsyncEnumerable<Obj> WrapParallelAsync(IAsyncEnumerable<Obj> asyncEnumerable)
    {
        ConcurrentQueue<Obj> bag = [];

        await Parallel.ForEachAsync(
            asyncEnumerable,
            TestContext.Current.CancellationToken,
            (obj, _) =>
            {
                bag.Enqueue(obj);
                return default;
            }
        );

        foreach (var obj in bag.OrderBy(o => o.Id))
            yield return obj;
    }
}
