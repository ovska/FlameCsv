using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.ParallelUtils;

internal sealed class ValueProducer<T, TValue> : IProducer<CsvRecordRef<T>, Accumulator<TValue>>
    where T : unmanaged, IBinaryInteger<T>
{
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ValueProducer<T, TValue> Create(CsvOptions<T> options, CsvParallelOptions parallelOptions)
    {
        return new(
            parallelOptions.EffectiveChunkSize,
            options,
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
            parallelOptions.EffectiveChunkSize,
            options ?? CsvOptions<T>.Default,
            typeMap.GetMaterializer,
            typeMap.GetMaterializer
        );

    public CsvOptions<T> Options { get; }

    private readonly int _chunkSize;
    private readonly Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> _materializerFactory;
    private readonly ManualResetEventSlim? _headerRead;
    private IMaterializer<T, TValue>? _materializer;
    private CancellationToken _cancellationToken;

    public ValueProducer(
        int chunkSize,
        CsvOptions<T> options,
        Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> materializerFactory,
        Func<CsvOptions<T>, IMaterializer<T, TValue>> headerlessFactory
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        _chunkSize = chunkSize;
        Options = options;
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
    }

    public void BeforeLoop(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public ValueTask BeforeLoopAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return ValueTask.CompletedTask;
    }

    public Accumulator<TValue> CreateState() => new(_chunkSize);

    public void Dispose()
    {
        _headerRead?.Dispose();
    }

    public void Produce(int order, CsvRecordRef<T> input, ref Accumulator<TValue> state)
    {
        if (_materializer is null)
        {
            if (order == 0)
            {
                ImmutableArray<string> headers = CsvHeader.Parse(input);
                _materializer = _materializerFactory(headers, Options);
                _headerRead!.Set();
                return;
            }
            else
            {
                _headerRead!.Wait(_cancellationToken);
            }
        }

        TValue result = _materializer!.Parse(input);
        state.Add(result);
    }

    public bool TryProduceDirect(int order, CsvRecordRef<T> input, [MaybeNullWhen(false)] out TValue? value)
    {
        if (_materializer is null)
        {
            if (order == 0)
            {
                ImmutableArray<string> headers = CsvHeader.Parse(input);
                _materializer = _materializerFactory(headers, Options);
                _headerRead!.Set();
                value = default;
                return false;
            }
            else
            {
                _headerRead!.Wait(_cancellationToken);
            }
        }

        value = _materializer!.Parse(input);
        return true;
    }
}
