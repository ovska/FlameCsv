namespace FlameCsv.Enumeration;

internal sealed class CopyingRecordAsyncEnumerable<T> : IAsyncEnumerable<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvRecordAsyncEnumerable<T> _source;

    public CopyingRecordAsyncEnumerable(in CsvRecordAsyncEnumerable<T> source)
    {
        _source = source;
    }

    public IAsyncEnumerator<CsvRecord<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(_source.GetAsyncEnumerator(cancellationToken));
    }

    private sealed class Enumerator : IAsyncEnumerator<CsvRecord<T>>
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
            if (await _source.MoveNextAsync().ConfigureAwait(false))
            {
                Current = new CsvRecord<T>(_source.Current);
                return true;
            }

            return false;
        }

        public ValueTask DisposeAsync() => _source.DisposeAsync();
    }
}
