using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
public sealed class CsvRecordEnumerator<T> : CsvRecordEnumeratorBase<T> where T : unmanaged, IEquatable<T>
{
    public CsvRecordEnumerator(
        ReadOnlyMemory<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.Reset(new ReadOnlySequence<T>(data));
    }

    public CsvRecordEnumerator(
        in ReadOnlySequence<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.Reset(in data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (MoveNextCore(isFinalBlock: false) || MoveNextCore(isFinalBlock: true))
        {
            return true;
        }

        // reached end of data
        _current = default;
        return false;
    }
}
