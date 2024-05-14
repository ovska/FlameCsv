using System.Buffers;
using System.Collections;
using FlameCsv.Binding;

namespace FlameCsv.Enumeration;

public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    public CsvTypeMapEnumerable(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);

        _data = csv;
        _options = options;
        _typeMap = typeMap;
    }

    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(_data, _options, _typeMap);
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
