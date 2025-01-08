using FlameCsv.Binding;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV. Used through <see cref="CsvReader"/>.
/// </summary>
public sealed class CsvTypeMapAsyncEnumerable<T, TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvTypeMap<T, TValue> _typeMap;

    internal CsvTypeMapAsyncEnumerable(
        ICsvPipeReader<T> reader,
        CsvOptions<T> options,
        CsvTypeMap<T, TValue> typeMap)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        _options = options;
        _reader = reader;
        _typeMap = typeMap;
    }

    [MustDisposeResource]
    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(_options, _typeMap, _reader, cancellationToken);
    }

    [MustDisposeResource]
    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
