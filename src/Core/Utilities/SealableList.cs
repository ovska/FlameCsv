using System.Collections;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Utilities;

file sealed class ReadOnlyOwner : ICanBeReadOnly
{
    public bool IsReadOnly => true;
}

internal interface IWrapper<T, TActual>
{
    static abstract TActual Wrap(T value);
    static abstract T Unwrap(TActual value);
}

internal sealed class SealableList<T, TActual> : IList<T>
    where TActual : IWrapper<T, TActual>
{
    public static SealableList<T, TActual> Empty { get; } = new(new ReadOnlyOwner(), null);

    private readonly ICanBeReadOnly _owner;
    internal readonly List<TActual> _list;

    public ReadOnlySpan<TActual> Span => _list.AsSpan();

    public SealableList(ICanBeReadOnly owner, SealableList<T, TActual>? defaultValues)
    {
        _owner = owner;
        _list = [];

        if (defaultValues is { Count: > 0 })
        {
            _list.AddRange(defaultValues._list);
        }
    }

    public SealableList<T, TActual> Clone() => new(_owner, this);

    public T this[int index]
    {
        get => TActual.Unwrap(_list[index]);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _owner.ThrowIfReadOnly();
            _list[index] = TActual.Wrap(value);
        }
    }

    public int Count => _list.Count;
    public bool IsReadOnly => _owner.IsReadOnly;

    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Add(TActual.Wrap(item));
    }

    public void Clear()
    {
        _owner.ThrowIfReadOnly();
        _list.Clear();
    }

    public bool Contains(T item) => _list.Contains(TActual.Wrap(item));

    public void CopyTo(T[] array, int arrayIndex)
    {
        for (int i = 0; i < _list.Count; i++)
        {
            array[arrayIndex + i] = TActual.Unwrap(_list[i]);
        }
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _list.Select(TActual.Unwrap).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    public int IndexOf(T item) => _list.IndexOf(TActual.Wrap(item));

    public void Insert(int index, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Insert(index, TActual.Wrap(item));
    }

    public bool Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        return _list.Remove(TActual.Wrap(item));
    }

    public void RemoveAt(int index)
    {
        _owner.ThrowIfReadOnly();
        _list.RemoveAt(index);
    }
}
