using System.Collections.Concurrent;
using System.Collections.Immutable;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.ParallelUtils;

internal sealed class ValueProducer<T, TValue> : IProducer<CsvRecordRef<T>, ChunkManager<TValue>>
    where T : unmanaged, IBinaryInteger<T>
{
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ValueProducer<T, TValue> Create(CsvOptions<T>? options, CsvParallelOptions parallelOptions)
    {
        return new(
            parallelOptions.ChunkSize ?? CsvParallel.DefaultChunkSize,
            options ?? CsvOptions<T>.Default,
            static (h, o) => o.TypeBinder.GetMaterializer<TValue>(h),
            static o => o.TypeBinder.GetMaterializer<TValue>()
        );
    }

    public static ValueProducer<T, TValue> Create(
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T>? options,
        CsvParallelOptions parallelOptions
    ) =>
        new(
            parallelOptions.ChunkSize ?? CsvParallel.DefaultChunkSize,
            options ?? CsvOptions<T>.Default,
            typeMap.GetMaterializer,
            typeMap.GetMaterializer
        );

    private readonly int _chunkSize;
    private readonly CsvOptions<T> _options;
    private readonly Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> _materializerFactory;
    private readonly ManualResetEventSlim? _headerRead;
    private IMaterializer<T, TValue>? _materializer;

    public ValueProducer(
        int chunkSize,
        CsvOptions<T> options,
        Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> materializerFactory,
        Func<CsvOptions<T>, IMaterializer<T, TValue>> headerlessFactory
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        _chunkSize = chunkSize;
        _options = options;
        _materializerFactory = materializerFactory;

        if (!options.HasHeader)
        {
            _materializer = headerlessFactory(options);
            _headerRead = null!;
        }
        else
        {
            _headerRead = new ManualResetEventSlim();
        }

        _pool = [];
        _release = arr => _pool.Push(arr);
    }

    private readonly ConcurrentStack<TValue[]> _pool;
    private readonly Action<TValue[]> _release;

    public void BeforeLoop() { }

    public ValueTask BeforeLoopAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ChunkManager<TValue> CreateState()
    {
        if (!_pool.TryPop(out TValue[]? array))
        {
            array = new TValue[_chunkSize];
        }

        return new ChunkManager<TValue>(array, _release);
    }

    public void Dispose()
    {
        _pool.Clear(); // clear possible references
        _headerRead?.Dispose();
    }

    public void Produce(int order, CsvRecordRef<T> input, ref ChunkManager<TValue> state)
    {
        if (_materializer is null)
        {
            if (order == 0)
            {
                ImmutableArray<string> headers = CsvHeader.Parse<T, CsvRecordRef<T>>(ref input);
                _materializer = _materializerFactory(headers, _options);
                _headerRead!.Set();
                return;
            }
            else
            {
                _headerRead!.Wait();
            }
        }

        TValue result = _materializer!.Parse(ref input);
        state.Add(result);
    }
}
