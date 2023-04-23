using System.Collections;

namespace FlameCsv;

internal sealed class CopyingRecordAsyncEnumerable<T> : IAsyncEnumerable<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvRecordAsyncEnumerable<T> _source;

    public CopyingRecordAsyncEnumerable(CsvRecordAsyncEnumerable<T> source)
    {
        _source = source;
    }

    public IAsyncEnumerator<CsvRecord<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(_source.GetAsyncEnumerator(cancellationToken));
    }

    public sealed class Enumerator : IAsyncEnumerator<CsvRecord<T>>
    {
        private readonly CsvRecordAsyncEnumerator<T> _source;

        public Enumerator(CsvRecordAsyncEnumerator<T> source)
        {
            _source = source;
            Current = default!;
        }

        public CsvRecord<T> Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (await _source.MoveNextAsync())
            {
                Current = new CsvRecord<T>(_source.Current);
                return true;
            }

            return false;
        }

        public ValueTask DisposeAsync() => _source.DisposeAsync();
    }
}

internal sealed class CopyingRecordEnumerable<T> : IEnumerable<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvRecordEnumerable<T> _source;

    public CopyingRecordEnumerable(CsvRecordEnumerable<T> source)
    {
        _source = source;
    }

    public IEnumerator<CsvRecord<T>> GetEnumerator() => new Enumerator(_source.GetEnumerator());

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public sealed class Enumerator : IEnumerator<CsvRecord<T>>
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

        public void Reset() => throw new NotImplementedException();
    }
}


