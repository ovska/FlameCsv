using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Utilities;

internal sealed class TypeStringDictionary : TypeDictionary<string?, string?>
{
    public TypeStringDictionary(ISealable owner, TypeStringDictionary? source = null) : base(owner, source)
    {
    }

    protected override string? InitializeValue(string? value) => value;
}

internal sealed class TypeByteDictionary : TypeDictionary<string?, ReadOnlyMemory<byte>>
{
    public TypeByteDictionary(ISealable owner, TypeByteDictionary? source = null) : base(owner, source)
    {
    }

    protected override ReadOnlyMemory<byte> InitializeValue(string? value) => CsvDialectStatic.AsBytes(value);
}

internal abstract class TypeDictionary<TValue, TResult> : IDictionary<Type, TValue>
{
    public bool TryGetInternalValue(Type key, out TResult result)
    {
        _owner.MakeReadOnly();

        if (_dictionary.TryGetValue(key, out TValue? value))
        {
            result = InitializeValue(value);
            return true;
        }

        result = default!;
        return false;
    }

    private readonly ISealable _owner;
    private readonly Dictionary<Type, TValue> _dictionary;

    protected TypeDictionary(
        ISealable owner,
        TypeDictionary<TValue, TResult>? source = null)
    {
        _owner = owner;
        _dictionary = source is null ? new() : new(source._dictionary);
    }

    protected abstract TResult InitializeValue(TValue value);

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

    ICollection<Type> IDictionary<Type, TValue>.Keys => throw new NotSupportedException();
    ICollection<TValue> IDictionary<Type, TValue>.Values => throw new NotSupportedException();

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
        _owner.ThrowIfReadOnly();
        return _dictionary.Remove(key);
    }

    public bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue value)
    {
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
                $"Null tokens are only valid for concrete types that can be null (was: {type.FullName})");
            // ^ TODO: use ToTypeString once open generics bug is fixed
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
