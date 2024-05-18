using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV. Used through <see cref="CsvReader"/>.
/// </summary>
[RequiresUnreferencedCode(Messages.CompiledExpressions)]
public sealed class CsvValueAsyncEnumerable<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
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

    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(_options, _reader, cancellationToken);
    }

    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
