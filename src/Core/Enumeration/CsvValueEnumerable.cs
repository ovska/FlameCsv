using System.Collections;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
internal sealed class CsvValueEnumerable<T, [DAM(Messages.ReflectionBound)] TValue>
    : IEnumerable<TValue>,
        IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Csv.IReadBuilder<T> _builder;
    private readonly CsvOptions<T> _options;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Creates a new instance that can be used to read CSV records.
    /// </summary>
    public CsvValueEnumerable(Csv.IReadBuilder<T> builder, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        _builder = builder;
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
        return new CsvValueEnumerator<T, TValue>(_options, _builder.CreateReader(false))
        {
            ExceptionHandler = _exceptionHandler,
        };
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueEnumerator<T, TValue>(_options, _builder.CreateReader(true), cancellationToken)
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
