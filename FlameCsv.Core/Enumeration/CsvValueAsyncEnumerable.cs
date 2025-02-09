using System.ComponentModel;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV. Used through <see cref="CsvReader"/>.
/// </summary>
[PublicAPI]
[RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
public sealed class CsvValueAsyncEnumerable<T, [DAM(Messages.ReflectionBound)] TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ICsvPipeReader<T> _reader;
    private CsvExceptionHandler<T>? _exceptionHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvValueAsyncEnumerable{T,TValue}"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public CsvValueAsyncEnumerable(ICsvPipeReader<T> reader, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        _reader = reader;
        _options = options;
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(_options, _reader, cancellationToken)
        {
            ExceptionHandler = _exceptionHandler
        };
    }

    [MustDisposeResource]
    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }

    /// <inheritdoc cref="CsvValueEnumerable{T,TValue}.WithExceptionHandler"/>
    public CsvValueAsyncEnumerable<T, TValue> WithExceptionHandler(CsvExceptionHandler<T>? handler)
    {
        _exceptionHandler = handler;
        return this;
    }
}
