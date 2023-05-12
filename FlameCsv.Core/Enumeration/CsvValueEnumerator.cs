using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

public sealed class CsvValueEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IEnumerator<TValue>
    where T : unmanaged, IEquatable<T>
{
    private ReadOnlySequence<T> _remaining;

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumerator(ReadOnlySequence<T> csv, in CsvReadingContext<T> context, IMaterializer<T, TValue>? materializer)
        : base(in context, materializer)
    {
        _remaining = csv;
    }

    internal CsvValueEnumerator(ReadOnlySequence<T> csv, in CsvReadingContext<T> context, CsvTypeMap<T, TValue> typeMap)
        : base(in context, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _remaining = csv;
    }

    public bool MoveNext()
    {
        return TryRead(ref _remaining, false) || TryRead(ref _remaining, true);
    }

    object IEnumerator.Current => Current!;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
