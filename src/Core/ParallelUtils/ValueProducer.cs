using System.Collections.Immutable;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.ParallelUtils;

internal sealed class ValueProducer<T, TValue> : IProducer<CsvRecordRef<T>, List<TValue>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly int _chunkSize;
    private readonly CsvOptions<T> _options;
    private readonly Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> _materializerFactory;
    private readonly ManualResetEventSlim? _headerRead;
    private IMaterializer<T, TValue>? _materializer;

    public ValueProducer(int chunkSize, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(chunkSize, options, typeMap.GetMaterializer, typeMap.GetMaterializer) { }

    public ValueProducer(
        int chunkSize,
        CsvOptions<T> options,
        Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> materializerFactory,
        Func<CsvOptions<T>, IMaterializer<T, TValue>> headerlessFactory
    )
    {
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
    }

    public void BeforeLoop() { }

    public ValueTask BeforeLoopAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public List<TValue> CreateState()
    {
        return new List<TValue>(_chunkSize);
    }

    public void Dispose()
    {
        _headerRead?.Dispose();
    }

    public void OnException(Exception exception)
    {
        exception.Rethrow(); // mimic what CsvValueEnumerator does
    }

    public void Produce(int order, CsvRecordRef<T> input, ref List<TValue> state)
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
