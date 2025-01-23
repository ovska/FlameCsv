using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> without reflection.
/// </summary>
public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
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

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(in _data, _options, _typeMap);
    }

    [MustDisposeResource] IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
    [MustDisposeResource] IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
