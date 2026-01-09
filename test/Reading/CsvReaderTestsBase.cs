using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public abstract class CsvReaderTestsBase
{
    private static IEnumerable<(CsvNewline, Header, int, Escaping, bool, Tokenizer, PoisonPagePlacement)> BaseData =>
        from crlf in (CsvNewline[])[CsvNewline.CRLF, CsvNewline.LF]
        from writeHeader in GlobalData.Enum<Header>()
        from bufferSize in (int[])[-1, 256, 4096]
        from escaping in GlobalData.Enum<Escaping>()
        from sourceGen in GlobalData.Booleans
        from tokenizer in GlobalData.Enum<Tokenizer>()
        from guarded in GlobalData.PoisonPlacement
        select (crlf, writeHeader, bufferSize, escaping, sourceGen, tokenizer, guarded);

    public static TheoryData<
        CsvNewline,
        Header,
        int,
        Escaping,
        bool,
        Tokenizer,
        PoisonPagePlacement
    > RecordData { get; } = [.. BaseData];

    public static TheoryData<
        CsvNewline,
        Header,
        int,
        Escaping,
        bool,
        bool,
        Tokenizer,
        PoisonPagePlacement
    > ObjectData { get; } =
    [
        .. from tuple in BaseData
        from parallel in GlobalData.Booleans
        select (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, parallel, tuple.Item6, tuple.Item7),
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
        Header header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool parallel,
        Tokenizer tokenizer,
        PoisonPagePlacement placement
    )
    {
        using var pool = new ReturnTrackingBufferPool(placement);
        CsvOptions<T> options = GetOptions(newline, header, escaping, tokenizer);
        var memory = TestDataGenerator.Generate<T>(newline, header is Header.Yes, escaping);

        using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
        {
            IEnumerable<Obj> enumerable;

            if (parallel)
            {
                var parallelBuilder = Csv.From(in sequence).AsParallel(GetParallelOptions());
                enumerable = (
                    sourceGen
                        ? parallelBuilder.ReadUnordered<Obj>(TypeMap, options)
                        : parallelBuilder.ReadUnordered<Obj>(options)
                ).SelectMany(s => s);
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
        Header header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        Tokenizer tokenizer,
        PoisonPagePlacement placement
    )
    {
        using var pool = new ReturnTrackingBufferPool(placement);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping, tokenizer);

            var memory = TestDataGenerator.Generate<T>(newline, header is Header.Yes, escaping);
            using (MemorySegment<T>.Create(memory, bufferSize, 0, pool, out var sequence))
            {
                var builder = Csv.From(in sequence);

                await foreach (var item in GetItems(builder, options, sourceGen, header, newline, isAsync: false))
                {
                    yield return item;
                }
            }
        }
    }

    [Theory, MemberData(nameof(ObjectData))]
    public async Task Objects_Async(
        CsvNewline newline,
        Header header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        bool parallel,
        Tokenizer tokenizer,
        PoisonPagePlacement placement
    )
    {
        using var pool = new ReturnTrackingBufferPool(placement);
        await Validate(Enumerate(), escaping, parallel);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping, tokenizer);

            var data = TestDataGenerator.Generate<byte>(newline, header is Header.Yes, escaping);

            await using var stream = data.AsStream();
            var builder = GetBuilder(stream, options, bufferSize, pool);

            IAsyncEnumerable<Obj> source;

            if (parallel)
            {
                source = ParallelCore();

#if !NET10_0_OR_GREATER
                async
#endif
                IAsyncEnumerable<Obj> ParallelCore()
                {
                    var parallelBuilder = builder.AsParallel(GetParallelOptions());
                    var enumerable = sourceGen
                        ? parallelBuilder.ReadUnorderedAsync<Obj>(TypeMap, options)
                        : parallelBuilder.ReadUnorderedAsync<Obj>(options);

#if NET10_0_OR_GREATER
                    return enumerable.SelectMany(s => s);
#else
                    await foreach (var segment in enumerable.WithTestContext())
                    {
                        await Task.Yield();

                        foreach (var item in segment)
                        {
                            yield return item;
                        }
                    }
#endif
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
        Header header,
        int bufferSize,
        Escaping escaping,
        bool sourceGen,
        Tokenizer tokenizer,
        PoisonPagePlacement placement
    )
    {
        using var pool = new ReturnTrackingBufferPool(placement);
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping, tokenizer);

            var data = TestDataGenerator.Generate<byte>(newline, header is Header.Yes, escaping);
            await using var stream = data.AsStream();
            var builder = GetBuilder(stream, options, bufferSize, pool);

            await foreach (var item in GetItems(builder, options, sourceGen, header, newline, isAsync: true))
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
#if NET10_0_OR_GREATER
            enumerable = enumerable.Order();
#else
            List<Obj> list = await SyncAsyncEnumerable.ToListAsync(enumerable);
            list.Sort();
            enumerable = SyncAsyncEnumerable.Create(list);
#endif
        }

        await foreach (var obj in enumerable.WithTestContext())
        {
            Assert.Equal(i, obj.Id);
            Assert.Equal(escaping is Escaping.Quote ? $"Name\"{i}" : $"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator.GuidBytes), obj.Token);

            i++;
        }

        Assert.Equal(1_000, i);
    }

    protected async IAsyncEnumerable<Obj> GetItems(
        Csv.IReadBuilder<T> builder,
        CsvOptions<T> options,
        bool sourceGen,
        Header header,
        CsvNewline newline,
        bool isAsync
    )
    {
        int index = 0;
        long tokenPosition = 0;
        bool hasHeader = header is Header.Yes;

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
                yield return Core(in record);
            }
        }
        else
        {
            foreach (var record in new CsvRecordEnumerable<T>(builder, options))
            {
                yield return Core(in record);
            }
        }

        Obj Core(in CsvRecord<T> record)
        {
            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            int length = record._owner.Reader._recordBuffer.GetLengthWithNewline(record._view);
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

    protected static CsvOptions<T> GetOptions(CsvNewline newline, Header header, Escaping escaping, Tokenizer tokenizer)
    {
        CsvOptions<T> options = new()
        {
            Formats = { [typeof(DateTime)] = "O" },
            Newline = newline,
            HasHeader = header is Header.Yes,
            Quote = escaping is Escaping.QuoteNull ? null : '"',
        };

        if (options.GetTokenizers().simd is null && tokenizer is not Tokenizer.Scalar)
        {
            Assert.Skip("SIMD tokenizers is not supported on this platform.");
        }

        if (tokenizer is Tokenizer.Scalar)
        {
            SimdTokenizerAccessor(options) = null;
        }
#if FULL_TEST_SUITE
        else if (tokenizer is not Tokenizer.Platform)
        {
            Assert.SkipUnless(
                Tokenizers.IsSupported(tokenizer),
                $"{tokenizer} tokenizer is not supported on this platform."
            );

            SimdTokenizerAccessor(options) = Tokenizers.GetTokenizer<T>(tokenizer, options);
        }
#endif

        return options;
    }

    private static CsvParallelOptions GetParallelOptions()
    {
        return new CsvParallelOptions { CancellationToken = TestContext.Current.CancellationToken };
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_simdTokenizer")]
    private static extern ref CsvTokenizer<T>? SimdTokenizerAccessor(CsvOptions<T> options);
}
