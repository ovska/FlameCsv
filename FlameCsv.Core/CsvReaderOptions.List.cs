using System.Collections;
using CommunityToolkit.HighPerformance;
using FlameCsv.Parsers;

namespace FlameCsv;

public partial class CsvReaderOptions<T>
{
    private sealed class ParserList : IList<ICsvParser<T>>
    {
        private readonly CsvReaderOptions<T> _options;
        private readonly List<ICsvParser<T>> _list;

        public ReadOnlySpan<ICsvParser<T>> Span => _list.AsSpan();

        public ParserList(CsvReaderOptions<T> options)
        {
            _options = options;
            _list = new List<ICsvParser<T>>(options.GetDefaultParsers());
        }

        public ICsvParser<T> this[int index]
        {
            get => _list[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _options.ThrowIfReadOnly();
                _list[index] = value;
            }
        }

        public int Count => _list.Count;
        public bool IsReadOnly => _options.IsReadOnly;

        public void Add(ICsvParser<T> item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _options.ThrowIfReadOnly();
            _list.Add(item);
        }

        public void Clear()
        {
            _options.ThrowIfReadOnly();
            _list.Clear();
        }

        public bool Contains(ICsvParser<T> item) => _list.Contains(item);

        public void CopyTo(ICsvParser<T>[] array, int arrayIndex) => throw new NotSupportedException();

        public IEnumerator<ICsvParser<T>> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public int IndexOf(ICsvParser<T> item) => _list.IndexOf(item);

        public void Insert(int index, ICsvParser<T> item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _options.ThrowIfReadOnly();
            _list.Insert(index, item);
        }

        public bool Remove(ICsvParser<T> item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _options.ThrowIfReadOnly();
            return _list.Remove(item);
        }

        public void RemoveForType(Type type) => _list.RemoveAll(p => p.CanParse(type));

        public void RemoveAt(int index)
        {
            _options.ThrowIfReadOnly();
            _list.RemoveAt(index);
        }

    }
}
