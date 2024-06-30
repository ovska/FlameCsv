using System.Collections;

namespace FlameCsv.Enumeration;

internal sealed class CopyingRecordEnumerable<T> : IEnumerable<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvRecordEnumerable<T> _source;

    public CopyingRecordEnumerable(in CsvRecordEnumerable<T> source)
    {
        _source = source;
    }

    public IEnumerator<CsvRecord<T>> GetEnumerator() => new Enumerator(_source.GetEnumerator());

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Enumerator : IEnumerator<CsvRecord<T>>
    {
        private readonly CsvRecordEnumerator<T> _source;

        public Enumerator(CsvRecordEnumerator<T> source)
        {
            _source = source;
            Current = default!;
        }

        public CsvRecord<T> Current { get; private set; }
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_source.MoveNext())
            {
                Current = new CsvRecord<T>(_source.Current);
                return true;
            }

            return false;
        }

        public void Dispose() => _source.Dispose();

        public void Reset() => throw new NotSupportedException();
    }
}
