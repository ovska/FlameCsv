using System.Buffers;
using System.Collections;
using FlameCsv.IO;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueEnumerable<T, [DAM(Messages.ReflectionBound)] TValue>
    : IEnumerable<TValue>,
        IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ReaderFactory<T> _reader;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
        : this(CsvBufferReader.Create(csv), options) { }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
        : this(CsvBufferReader.Create(in csv), options) { }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(ICsvBufferReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        _reader = new(reader);
        _options = options;
    }

    internal CsvValueEnumerable(ReaderFactory<T> factory, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _reader = factory;
        _options = options;
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(_options, _reader.Create(false))
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueEnumerator<T, TValue>(_options, _reader.Create(true), cancellationToken)
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    /// <summary>
    /// Sets the exception handler for the enumerator.
    /// If the handler returns <c>true</c>, the exception is considered handled and the record is skipped.
    /// </summary>
    /// <param name="handler">Exception handler. Set to null to remove an existing handler</param>
    /// <returns>The same enumerable instance</returns>
    public CsvValueEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler)
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
