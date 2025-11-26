using System.Collections;
using FlameCsv.Binding;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[PublicAPI]
public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue>, IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Csv.IReadBuilderBase<T> _builder;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private CsvExceptionHandler<T>? _exceptionHandler;

    internal CsvTypeMapEnumerable(Csv.IReadBuilderBase<T> factory, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        _typeMap = typeMap;
        _builder = factory;
        _options = options;
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvTypeMapEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvTypeMapEnumerator<T, TValue>(_options, _typeMap, _builder.CreateReader(false))
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvTypeMapEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvTypeMapEnumerator<T, TValue>(_options, _typeMap, _builder.CreateReader(true), cancellationToken)
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    /// <inheritdoc cref="CsvValueEnumerable{T,TValue}.WithExceptionHandler"/>
    public CsvTypeMapEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler)
    {
        _exceptionHandler = handler;
        return this;
    }

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MustDisposeResource]
    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
