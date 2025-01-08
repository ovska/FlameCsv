using System.Collections;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Utilities;

internal sealed class SealableList<T> : IList<T>
{
    private readonly ICanBeReadOnly _owner;
    private readonly List<T> _list;

    public ReadOnlySpan<T> Span => _list.AsSpan();

    public SealableList(ICanBeReadOnly owner, SealableList<T>? defaultValues)
    {
        _owner = owner;
        _list = [];

        if (defaultValues is { Count: > 0 })
        {
            _list.AddRange(defaultValues._list);
        }
    }

    public T this[int index]
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

    public void Add(T item)
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

    public bool Contains(T item) => _list.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public int IndexOf(T item) => _list.IndexOf(item);

    public void Insert(int index, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Insert(index, item);
    }

    public bool Remove(T item)
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
}
