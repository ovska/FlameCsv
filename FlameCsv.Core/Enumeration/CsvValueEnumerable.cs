using System.Buffers;
using System.Collections;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerable that can be used to read <typeparamref name="TValue"/> using reflection.
/// </summary>
[RUF(Messages.CompiledExpressions)]
public sealed class CsvValueEnumerable<T, [DAM(Messages.ReflectionBound)] TValue> : IEnumerable<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CsvValueEnumerable(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = csv;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MustDisposeResource]
    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(_data, _options, materializer: null);
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
