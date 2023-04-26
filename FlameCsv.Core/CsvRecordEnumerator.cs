using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv;

/// <inheritdoc cref="CsvEnumeratorBase{T}"/>
public sealed class CsvRecordEnumerator<T> : CsvEnumeratorBase<T> where T : unmanaged, IEquatable<T>
{
    private ReadOnlySequence<T> _data;

    public CsvRecordEnumerator(ReadOnlyMemory<T> data, CsvReaderOptions<T> options) : base(options)
    {
        _data = new ReadOnlySequence<T>(data);
    }

    public CsvRecordEnumerator(in ReadOnlySequence<T> data, CsvReaderOptions<T> options) : base(options)
    {
        _data = data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (MoveNextCore(ref _data, isFinalBlock: false) ||
            MoveNextCore(ref _data, isFinalBlock: true))
        {
            return true;
        }

        // reached end of data
        Position += _current.RawRecord.Length;
        _current = default;
        return false;
    }
}
