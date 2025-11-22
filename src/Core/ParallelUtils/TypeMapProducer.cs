using System.Collections.Immutable;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.ParallelUtils;

internal sealed class TypeMapProducer<T, TValue> : IProducer<CsvRecordRef<T>, List<TValue>>, IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly int _chunkSize;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private readonly ManualResetEventSlim? _headerRead;
    private IMaterializer<T, TValue>? _materializer;

    public TypeMapProducer(int chunkSize, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);

        _chunkSize = chunkSize;
        _options = options;
        _typeMap = typeMap;

        if (!options.HasHeader)
        {
            _materializer = _typeMap.GetMaterializer(options);
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

    public void Produce(CsvRecordRef<T> input, ref List<TValue> state)
    {
        if (_materializer is null)
        {
            if (input._isFirst)
            {
                ImmutableArray<string> headers = CsvHeader.Parse<T, CsvRecordRef<T>>(ref input);
                _materializer = _typeMap.GetMaterializer(headers, _options);
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
