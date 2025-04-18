using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using FlameCsv.IO;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[PublicAPI]
public sealed class CsvTypeMapEnumerable<T, TValue> : IEnumerable<TValue>, IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ICsvBufferReader<T> _reader;
    private readonly CsvOptions<T> _options;
    private readonly CsvTypeMap<T, TValue> _typeMap;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvTypeMapEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(CsvBufferReader.Create(csv), options, typeMap)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvTypeMapEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(CsvBufferReader.Create(in csv), options, typeMap)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvTypeMapEnumerable(
        ICsvBufferReader<T> reader,
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        _typeMap = typeMap;
        _reader = reader;
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
        return new CsvTypeMapEnumerator<T, TValue>(_options, _typeMap, _reader)
        {
            ExceptionHandler = _exceptionHandler
        };
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvTypeMapEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvTypeMapEnumerator<T, TValue>(_options, _typeMap, _reader, cancellationToken)
        {
            ExceptionHandler = _exceptionHandler
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
