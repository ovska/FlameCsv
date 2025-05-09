using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;
using FlameCsv.Utilities.Comparers;

namespace FlameCsv.Utilities;

internal class TypeStringDictionary : TypeDictionary<Utf8String?>, IDictionary<Type, string?>
{
    public TypeStringDictionary(ICanBeReadOnly owner, TypeDictionary<Utf8String?>? source = null)
        : base(owner, source) { }

    IEnumerator<KeyValuePair<Type, string?>> IEnumerable<KeyValuePair<Type, string?>>.GetEnumerator()
    {
        return _dictionary.Select(kvp => new KeyValuePair<Type, string?>(kvp.Key, kvp.Value?.String)).GetEnumerator();
    }

    void ICollection<KeyValuePair<Type, string?>>.Add(KeyValuePair<Type, string?> item)
    {
        ((ICollection<KeyValuePair<Type, Utf8String?>>)this).Add(new(item.Key, item.Value));
    }

    bool ICollection<KeyValuePair<Type, string?>>.Contains(KeyValuePair<Type, string?> item)
    {
        return ((ICollection<KeyValuePair<Type, Utf8String?>>)this).Contains(new(item.Key, item.Value));
    }

    void ICollection<KeyValuePair<Type, string?>>.CopyTo(KeyValuePair<Type, string?>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Count + arrayIndex, array.Length);

        foreach (var kvp in _dictionary)
        {
            array[arrayIndex++] = new KeyValuePair<Type, string?>(kvp.Key, kvp.Value);
        }
    }

    bool ICollection<KeyValuePair<Type, string?>>.Remove(KeyValuePair<Type, string?> item)
    {
        return ((ICollection<KeyValuePair<Type, Utf8String?>>)this).Remove(new(item.Key, item.Value));
    }

    void IDictionary<Type, string?>.Add(Type key, string? value)
    {
        base.Add(key, value);
    }

    bool IDictionary<Type, string?>.TryGetValue(Type key, out string? value)
    {
        if (base.TryGetValue(key, out var utf8Value))
        {
            value = utf8Value?.String;
            return true;
        }

        value = null;
        return false;
    }

    string? IDictionary<Type, string?>.this[Type key]
    {
        get => base[key];
        set => base[key] = value;
    }

    ICollection<Type> IDictionary<Type, string?>.Keys => ((IDictionary<Type, Utf8String?>)this).Keys;
    ICollection<string?> IDictionary<Type, string?>.Values => _dictionary.Values.Select(x => x?.String).ToList();
}

internal class TypeDictionary<TValue> : IDictionary<Type, TValue>
{
    public TypeDictionary<TValue> Clone()
    {
        return new TypeDictionary<TValue>(_owner, this);
    }

    private protected readonly ICanBeReadOnly _owner;
    private protected readonly Dictionary<Type, TValue> _dictionary;

    public TypeDictionary(ICanBeReadOnly owner, TypeDictionary<TValue>? source = null)
    {
        _owner = owner;
        _dictionary = source is null
            ? new(NullableTypeEqualityComparer.Instance)
            : new(source._dictionary, NullableTypeEqualityComparer.Instance);
    }

    public TValue this[Type key]
    {
        get => _dictionary[key];
        set
        {
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
        return _dictionary.ContainsKey(key);
    }

    public bool Remove(Type key)
    {
        ValidateKey(key);
        _owner.ThrowIfReadOnly();
        return _dictionary.Remove(key);
    }

    public bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _dictionary.TryGetValue(key, out value);
    }

    protected static void ValidateKey(Type key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.IsPointer || key.IsByRef || key.IsByRefLike || key.IsGenericTypeDefinition)
        {
            Throw.Argument(nameof(key), "Type must not be a pointer, byref/like, or a generic definition");
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
