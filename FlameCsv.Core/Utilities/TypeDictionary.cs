using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Utilities;

internal sealed class TypeDictionary<TValue, TAlternate> : IDictionary<Type, TValue>
{
    public TypeDictionary<TValue, TAlternate> Clone()
    {
        return new TypeDictionary<TValue, TAlternate>(
            _owner,
            _convert,
            this);
    }

    private readonly ISealable _owner;
    private readonly Dictionary<Type, TValue> _dictionary;
    private readonly Dictionary<Type, TAlternate>? _alternate;
    private readonly Func<TValue, TAlternate>? _convert;

    [MemberNotNullWhen(true, nameof(_alternate))]
    [MemberNotNullWhen(true, nameof(_convert))]
    public bool HasAlternate => _alternate is not null && _convert is not null;

    public TypeDictionary(
        ISealable owner,
        Func<TValue, TAlternate>? convert = null,
        TypeDictionary<TValue, TAlternate>? source = null)
    {
        _owner = owner;
        _dictionary = source is null ? new(NullableTypeEqualityComparer.Instance) : new(source._dictionary, NullableTypeEqualityComparer.Instance);

        if (typeof(TAlternate) == typeof(object))
        {
            _convert = null;
            _alternate = null;
        }
        else
        {
            _convert = convert ?? throw new InvalidOperationException($"Alternate missing for {typeof(TAlternate)}");
            _alternate = source?._alternate is null
                ? new Dictionary<Type, TAlternate>(NullableTypeEqualityComparer.Instance)
                : new Dictionary<Type, TAlternate>(source._alternate, NullableTypeEqualityComparer.Instance);
        }
    }

    public TValue this[Type key]
    {
        get => _dictionary[key];
        set
        {
            ValidateKey(key);
            _owner.ThrowIfReadOnly();
            _dictionary[key] = value;

            if (HasAlternate)
                _alternate[key] = _convert(value);
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

        if (HasAlternate)
            _alternate[key] = _convert(value);
    }

    public void Clear()
    {
        _owner.ThrowIfReadOnly();
        _dictionary.Clear();
        _alternate?.Clear();
    }

    public bool ContainsKey(Type key)
    {
        return _dictionary.ContainsKey(key);
    }

    public bool Remove(Type key)
    {
        ValidateKey(key);
        _owner.ThrowIfReadOnly();
        _alternate?.Remove(key);
        return _dictionary.Remove(key);
    }

    public bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _dictionary.TryGetValue(key, out value);
    }

    public bool TryGetAlternate(Type key, [MaybeNullWhen(false)] out TAlternate value)
    {
        ArgumentNullException.ThrowIfNull(key);
        value = default;
        return _alternate?.TryGetValue(key, out value) ?? false;
    }

    private static void ValidateKey(Type key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.IsPointer || key.IsByRef || key.IsByRefLike || key.IsGenericTypeDefinition)
        {
            ThrowHelper.ThrowArgumentException("key", (string?)null);
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
