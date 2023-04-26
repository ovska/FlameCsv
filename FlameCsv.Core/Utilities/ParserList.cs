using System.Collections;
using CommunityToolkit.HighPerformance;
using FlameCsv.Parsers;

namespace FlameCsv.Utilities;

internal sealed class ParserList<T> : IList<ICsvParser<T>> where T : unmanaged, IEquatable<T>
{
    private readonly ISealable _owner;
    private readonly List<ICsvParser<T>> _list;

    public ReadOnlySpan<ICsvParser<T>> Span => _list.AsSpan();

    public ParserList(CsvReaderOptions<T> owner, IEnumerable<ICsvParser<T>>? defaultValues)
    {
        _owner = owner;
        _list = defaultValues is null ? new List<ICsvParser<T>>() : new List<ICsvParser<T>>(defaultValues);
    }

    public ICsvParser<T> this[int index]
    {
        get => _list[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ThrowIfReadOnly();
            _list[index] = value;
        }
    }

    public int Count => _list.Count;
    public bool IsReadOnly => _owner.IsReadOnly;

    public void Add(ICsvParser<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Add(item);
    }

    public void Clear()
    {
        _owner.ThrowIfReadOnly();
        _list.Clear();
    }

    public bool Contains(ICsvParser<T> item) => _list.Contains(item);

    public void CopyTo(ICsvParser<T>[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public IEnumerator<ICsvParser<T>> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public int IndexOf(ICsvParser<T> item) => _list.IndexOf(item);

    public void Insert(int index, ICsvParser<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Insert(index, item);
    }

    public bool Remove(ICsvParser<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        return _list.Remove(item);
    }

    public void RemoveAt(int index)
    {
        _owner.ThrowIfReadOnly();
        _list.RemoveAt(index);
    }

    public int RemoveAll(Predicate<ICsvParser<T>> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        _owner.ThrowIfReadOnly();
        return _list.RemoveAll(match);
    }
}
