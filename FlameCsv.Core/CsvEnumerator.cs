using System.Buffers;
using System.Collections;

namespace FlameCsv;

public sealed class CsvEnumerator<T> : CsvEnumeratorBase<T>, IEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private ReadOnlySequence<T> _data;

    public CsvEnumerator(
        ReadOnlySequence<T> data,
        CsvReaderOptions<T> options,
        CancellationToken cancellationToken)
        : base(options, cancellationToken)
    {
        _data = data;
    }

    public bool MoveNext()
    {
        if (MoveNextCore(ref _data, isFinalBlock: false))
            return true;

        if (MoveNextCore(ref _data, isFinalBlock: true))
            return true;

        // reached end of data
        Position += Current.Data.Length;
        Current = default;

        return false;
    }

    void IEnumerator.Reset() => throw new NotSupportedException(); // TODO: preserve the original ReadOnlySequence<T> as well?
    object IEnumerator.Current => Current;
}