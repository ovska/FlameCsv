using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
public sealed class CsvRecordEnumerator<T> : CsvRecordEnumeratorBase<T>, IEnumerator<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    internal CsvRecordEnumerator(
        ReadOnlyMemory<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.Reset(new ReadOnlySequence<T>(data));
    }

    internal CsvRecordEnumerator(
        in ReadOnlySequence<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.Reset(in data);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (MoveNextCore(isFinalBlock: false) || MoveNextCore(isFinalBlock: true))
        {
            return true;
        }

        // reached the end of data
        _current = default;
        return false;
    }

    // RIDER complains about this class otherwise
    /// <inheritdoc />
    public new CsvValueRecord<T> Current => base.Current;

    object IEnumerator.Current => base.Current;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
