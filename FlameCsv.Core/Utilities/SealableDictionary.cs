using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Utilities;

internal sealed class SealableDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
{
    private readonly ISealable _owner;
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly Action<TKey> _validateKey;

    public SealableDictionary(ISealable owner, Action<TKey> validateKey)
    {
        _owner = owner;
        _dictionary = new();
        _validateKey = validateKey;
    }

    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set
        {
            _owner.ThrowIfReadOnly();
            _validateKey(key);
            _dictionary[key] = value;
        }
    }

    public ICollection<TKey> Keys => _dictionary.Keys;
    public ICollection<TValue> Values => _dictionary.Values;
    public int Count => _dictionary.Count;
    public bool IsReadOnly => _owner.IsReadOnly;

    public void Add(TKey key, TValue value)
    {
        _owner.ThrowIfReadOnly();
        _validateKey(key);
        _dictionary[key] = value;
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        _owner.ThrowIfReadOnly();
        _validateKey(item.Key);
        _dictionary.Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _owner.ThrowIfReadOnly();
        _dictionary.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return ((IDictionary<TKey, TValue>)_dictionary).Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

    public bool Remove(TKey key)
    {
        _owner.ThrowIfReadOnly();
        return _dictionary.Remove(key);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        _owner.ThrowIfReadOnly();
        return ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
