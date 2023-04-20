using System.Buffers;
using System.Collections;
using FlameCsv.Reading;

namespace FlameCsv;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style", "IDE0044:Add readonly modifier",
    Justification = "TProcessor is a mutable struct with non-readonly methods")]
internal sealed class CsvRecordEnumerator<T, TValue, TProcessor> : IEnumerator<TValue>
    where T : unmanaged, IEquatable<T>
    where TProcessor : struct, ICsvProcessor<T, TValue>
{
    public TValue Current => _current;

    private TValue _current;

    private TProcessor _processor;
    private ReadOnlySequence<T> _remaining;

    public CsvRecordEnumerator(ReadOnlySequence<T> csv, TProcessor processor)
    {
        _remaining = csv;
        _processor = processor;
        _current = default!;
    }

    public bool MoveNext()
    {
        if (_processor.TryRead(ref _remaining, out _current, false))
        {
            return true;
        }

        if (_processor.TryRead(ref _remaining, out _current, true))
        {
            return true;
        }

        _current = default!;
        return false;
    }

    public void Dispose()
    {
        _processor.Dispose();
    }

    object IEnumerator.Current => _current!;
    void IEnumerator.Reset() => throw new NotSupportedException();
}
