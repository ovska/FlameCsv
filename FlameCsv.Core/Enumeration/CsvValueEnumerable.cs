using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Enumeration;

[RequiresUnreferencedCode(Messages.CompiledExpressions)]
public sealed class CsvValueEnumerable<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue> : IEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvOptions<T> _options;

    public CsvValueEnumerable(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _data = csv;
        _options = options;
    }

    public CsvValueEnumerator<T, TValue> GetEnumerator()
    {
        return new CsvValueEnumerator<T, TValue>(_data, _options, materializer: null);
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
