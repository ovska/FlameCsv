using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
public sealed class CsvRecordEnumerator<T> : CsvRecordEnumeratorBase<T> where T : unmanaged, IEquatable<T>
{
    public CsvRecordEnumerator(
        ReadOnlyMemory<T> data,
        CsvOptions<T> options)
        : this(new ReadOnlySequence<T>(data), options)
    {
    }

    public CsvRecordEnumerator(
        in ReadOnlySequence<T> data,
        CsvOptions<T> options)
        : base(new CsvReadingContext<T>(options))
    {
        _data.Reset(in data);
    }

    internal CsvRecordEnumerator(in ReadOnlySequence<T> data, in CsvReadingContext<T> context) : base(in context)
    {
        _data.Reset(in data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (MoveNextCore(isFinalBlock: false) || MoveNextCore(isFinalBlock: true))
        {
            return true;
        }

        // reached end of data
        Position += _current.RawRecord.Length;
        _current = default;
        return false;
    }
}
