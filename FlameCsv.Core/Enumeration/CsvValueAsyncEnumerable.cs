using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV. Used through <see cref="CsvReader"/>.
/// </summary>
[RUF(Messages.CompiledExpressions)]
public sealed class CsvValueAsyncEnumerable<T, [DAM(Messages.ReflectionBound)] TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ICsvPipeReader<T> _reader;

    internal CsvValueAsyncEnumerable(
        ICsvPipeReader<T> reader,
        CsvOptions<T> options)
    {
        _reader = reader;
        _options = options;
    }

    /// <inheritdoc cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>
    [MustDisposeResource]
    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(_options, _reader, cancellationToken);
    }

    [MustDisposeResource]
    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
