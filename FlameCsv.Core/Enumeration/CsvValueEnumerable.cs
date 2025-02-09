using System.Buffers;
using System.Collections;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueEnumerable<T, [DAM(Messages.ReflectionBound)] TValue> : IEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CsvValueEnumerable(in ReadOnlySequence<T> csv, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = csv;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(in _data, _options)
        {
            ExceptionHandler = _exceptionHandler
        };
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
}
