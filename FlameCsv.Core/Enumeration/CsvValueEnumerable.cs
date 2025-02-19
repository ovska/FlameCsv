using System.Buffers;
using System.Collections;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueEnumerable<T, [DAM(Messages.ReflectionBound)] TValue>
    : IEnumerable<TValue>, ICsvValueAsyncEnumerable<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ICsvPipeReader<T> _reader;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(ReadOnlyMemory<T> csv, CsvOptions<T> options)
        : this(new ReadOnlySequence<T>(csv), options)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
        : this(new ConstantPipeReader<T>(in csv), options)
    {
    }

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(ICsvPipeReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        _reader = reader;
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
        return new CsvValueEnumerator<T, TValue>(_options, _reader) { ExceptionHandler = _exceptionHandler };
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueEnumerator<T, TValue>(_options, _reader, cancellationToken)
        {
            ExceptionHandler = _exceptionHandler
        };
    }

    /// <summary>
    /// Sets the exception handler for the enumerator.
    /// If the handler returns <see langword="true"/>, the exception is considered handled and the record is skipped.
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

    ICsvValueAsyncEnumerable<T, TValue> ICsvValueAsyncEnumerable<T, TValue>.WithExceptionHandler(
        CsvExceptionHandler<T>? handler)
    {
        return WithExceptionHandler(handler);
    }
}
