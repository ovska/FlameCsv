using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

internal sealed class MaterializerCache<T> where T : unmanaged, IEquatable<T>
{
    private readonly Dictionary<Type, object> _cache = new();
    private readonly CsvEnumerationState<T> _state;

    public MaterializerCache(CsvEnumerationState<T> state)
    {
        _state = state;
    }

    public TValue ParseValue<TValue>(CsvValueRecord<T> record)
    {
        Guard.IsReferenceEqualTo(_state, record._state);
        _state.EnsureVersion(record._version);

        if (!_cache.TryGetValue(typeof(TValue), out object? cached))
        {
            _cache[typeof(TValue)] = cached = _state._options.GetMaterializer<T, TValue>();
        }

        IMaterializer<T, TValue> materializer = (IMaterializer<T, TValue>)cached;

        CsvEnumerationStateRef<T> state = _state.GetInitialStateFor(record.Data, record._meta);
        return materializer.Parse(ref state);
    }
}
