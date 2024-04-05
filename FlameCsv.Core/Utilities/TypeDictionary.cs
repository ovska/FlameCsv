using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Utilities;

internal sealed class TypeDictionary<TValue> : IDictionary<Type, TValue>
{
    private readonly ISealable _owner;
    private readonly Dictionary<Type, TValue> _dictionary;

    public TypeDictionary(
        ISealable owner,
        TypeDictionary<TValue>? source = null)
    {
        _owner = owner;
        _dictionary = source is null ? new() : new(source._dictionary);
    }

    public TValue this[Type key]
    {
        get => _dictionary[key];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateKey(key);
            _owner.ThrowIfReadOnly();
            _dictionary[key] = value;
        }
    }

    public int Count => _dictionary.Count;
    public bool IsReadOnly => _owner.IsReadOnly;

    ICollection<Type> IDictionary<Type, TValue>.Keys => _dictionary.Keys;
    ICollection<TValue> IDictionary<Type, TValue>.Values => _dictionary.Values;

    public void Add(Type key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ValidateKey(key);
        _owner.ThrowIfReadOnly();
        _dictionary[key] = value;
    }

    public void Clear()
    {
        _owner.ThrowIfReadOnly();
        _dictionary.Clear();
    }

    public bool ContainsKey(Type key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _dictionary.ContainsKey(key);
    }

    public bool Remove(Type key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _owner.ThrowIfReadOnly();
        return _dictionary.Remove(key);
    }

    public bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _dictionary.TryGetValue(key, out value);
    }

    private static void ValidateKey(Type type)
    {
        if (type.IsPointer ||
            type.IsByRef ||
            type.IsGenericTypeDefinition ||
            (type.IsValueType && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))))
        {
            ThrowHelper.ThrowArgumentException(
                $"Null tokens are only valid for concrete types that can be null (was: {type.ToTypeString()})");
        }
    }

    void ICollection<KeyValuePair<Type, TValue>>.Add(KeyValuePair<Type, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    bool ICollection<KeyValuePair<Type, TValue>>.Contains(KeyValuePair<Type, TValue> item)
    {
        return ((ICollection<KeyValuePair<Type, TValue>>)_dictionary).Contains(item);
    }

    void ICollection<KeyValuePair<Type, TValue>>.CopyTo(KeyValuePair<Type, TValue>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<Type, TValue>>)_dictionary).CopyTo(array, arrayIndex);
    }

    bool ICollection<KeyValuePair<Type, TValue>>.Remove(KeyValuePair<Type, TValue> item)
    {
        return ((ICollection<KeyValuePair<Type, TValue>>)_dictionary).Remove(item);
    }

    IEnumerator<KeyValuePair<Type, TValue>> IEnumerable<KeyValuePair<Type, TValue>>.GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }
}
