using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> without reflection.
/// </summary>
[PublicAPI]
public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private CsvExceptionHandler<T>? _exceptionHandler;

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
    public CsvTypeMapEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvTypeMapEnumerator<T, TValue>(in _data, _options, _typeMap)
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    [MustDisposeResource]
    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="CsvValueEnumerable{T,TValue}.WithExceptionHandler"/>
    public CsvTypeMapEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler)
    {
        _exceptionHandler = handler;
        return this;
    }
}
