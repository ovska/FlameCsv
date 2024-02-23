using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
public sealed class CsvRecordEnumerator<T> : CsvRecordEnumeratorBase<T> where T : unmanaged, IEquatable<T>
{
    private ReadOnlySequence<T> _data;

    public CsvRecordEnumerator(
        ReadOnlyMemory<T> data,
        CsvOptions<T> options,
        CsvContextOverride<T> overrides = default)
        : base(new CsvReadingContext<T>(options, in overrides))
    {
        _data = new ReadOnlySequence<T>(data);
    }

    public CsvRecordEnumerator(
        in ReadOnlySequence<T> data,
        CsvOptions<T> options,
        CsvContextOverride<T> overrides = default)
        : base(new CsvReadingContext<T>(options, in overrides))
    {
        _data = data;
    }

    internal CsvRecordEnumerator(in ReadOnlySequence<T> data, in CsvReadingContext<T> context) : base(in context)
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
