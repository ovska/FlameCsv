using System.Collections;
using FlameCsv.Parsers;

namespace FlameCsv;

public partial class CsvReaderOptions<T>
{
    /// <summary>
    /// Returns a locking enumerator around the parsers.
    /// </summary>
    internal ParserEnumerator EnumerateParsers() => new(_parsers);

    internal struct ParserEnumerator : IEnumerator<ICsvParser<T>>
    {
        public readonly ParserEnumerator GetEnumerator() => this;

        private readonly List<ICsvParser<T>> _list;
        private List<ICsvParser<T>>.Enumerator _inner;
        private readonly bool _lockTaken;

        public ParserEnumerator(List<ICsvParser<T>> list)
        {
            _lockTaken = false;
            Monitor.Enter(list, ref _lockTaken);

            _list = list;
            _inner = list.GetEnumerator();
        }

        public bool MoveNext() => _inner.MoveNext();

        readonly void IEnumerator.Reset() => ((IEnumerator)_inner).Reset();
        public ICsvParser<T> Current => _inner.Current;
        object IEnumerator.Current => Current;

        public readonly void Dispose()
        {
            if (_lockTaken) Monitor.Exit(_list);
        }
    }
}
