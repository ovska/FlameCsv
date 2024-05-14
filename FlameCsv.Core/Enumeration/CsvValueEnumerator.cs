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
    internal CsvValueEnumerator(ReadOnlySequence<T> csv, CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
        : base(options, materializer)
    {
        _parser.Reset(in csv);
    }

    internal CsvValueEnumerator(ReadOnlySequence<T> csv, CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : base(options, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        _parser.Reset(in csv);
    }

    public bool MoveNext()
    {
        return TryRead(isFinalBlock: false) || TryRead(isFinalBlock: true);
    }

    object IEnumerator.Current => Current!;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
