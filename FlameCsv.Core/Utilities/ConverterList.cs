﻿using System.Collections;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Utilities;

internal sealed class ConverterList<T> : IList<CsvConverter<T>> where T : unmanaged, IEquatable<T>
{
    private readonly ISealable _owner;
    private readonly List<CsvConverter<T>> _list;

    public ReadOnlySpan<CsvConverter<T>> Span => _list.AsSpan();

    public ConverterList(CsvOptions<T> owner, ConverterList<T>? defaultValues)
    {
        nint a = IntPtr.Zero;
        nuint b = UIntPtr.Zero;

        _owner = owner;
        _list = new List<CsvConverter<T>>();

        if (defaultValues is { Count: > 0 })
        {
            _list.AddRange(defaultValues._list);
        }
    }

    public CsvConverter<T> this[int index]
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

    public void Add(CsvConverter<T> item)
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

    public bool Contains(CsvConverter<T> item) => _list.Contains(item);

    public void CopyTo(CsvConverter<T>[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public IEnumerator<CsvConverter<T>> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public int IndexOf(CsvConverter<T> item) => _list.IndexOf(item);

    public void Insert(int index, CsvConverter<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.ThrowIfReadOnly();
        _list.Insert(index, item);
    }

    public bool Remove(CsvConverter<T> item)
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

    public int RemoveAll(Predicate<CsvConverter<T>> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        _owner.ThrowIfReadOnly();
        return _list.RemoveAll(match);
    }
}