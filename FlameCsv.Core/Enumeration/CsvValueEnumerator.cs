using System.Buffers;
using System.Collections;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvValueAsyncEnumerator{T, TValue}"/>
public sealed class CsvValueEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    [RUF(Messages.CompiledExpressions)]
    internal CsvValueEnumerator(in ReadOnlySequence<T> csv, CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
        : base(options, materializer)
    {
        _parser.Reset(in csv);
    }

    internal CsvValueEnumerator(in ReadOnlySequence<T> csv, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : base(options, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _parser.Reset(in csv);
    }

    /// <inheritdoc cref="IEnumerator.MoveNext"/>
    public bool MoveNext()
    {
        return TryRead(isFinalBlock: false) || TryRead(isFinalBlock: true);
    }

    // RIDER complains about this class otherwise
    /// <inheritdoc cref="IEnumerator{T}.Current"/>
    public new TValue Current => base.Current;

    object IEnumerator.Current => Current!;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
