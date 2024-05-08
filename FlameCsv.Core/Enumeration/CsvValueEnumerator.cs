using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

public sealed class CsvValueEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IEnumerator<TValue>
    where T : unmanaged, IEquatable<T>
{
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumerator(ReadOnlySequence<T> csv, in CsvReadingContext<T> context, IMaterializer<T, TValue>? materializer)
        : base(in context, materializer)
    {
        _data.Reset(in csv);
    }

    internal CsvValueEnumerator(ReadOnlySequence<T> csv, in CsvReadingContext<T> context, CsvTypeMap<T, TValue> typeMap)
        : base(in context, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _data.Reset(in csv);
    }

    public bool MoveNext()
    {
        return TryRead(isFinalBlock: false) || TryRead(isFinalBlock: true);
    }

    object IEnumerator.Current => Current!;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
