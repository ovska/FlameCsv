using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Utilities;

public interface ITypeMap<TValue>
{
    TValue? this[Type key] { get; set; }
    void Add(Type key, TValue? value);
    int Count { get; }
    bool IsReadOnly { get; }
    void Clear();
    bool ContainsKey(Type key);
    bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue? value);
}

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



internal abstract class TypeDictionary<TValue, TResult> : ITypeMap<TValue>
{
    private readonly struct Wrapper
    {
        public Wrapper(TValue? value, TResult result)
        {
            Value = value;
            Result = result;
        }

        public TValue? Value { get; }
        public TResult Result { get; }
    }

    public bool TryGetInternalValue(Type key, out TResult result)
    {
        _owner.MakeReadOnly();

        ref Wrapper value = ref CollectionsMarshal.GetValueRefOrNullRef(_dictionary, key);

        if (Unsafe.IsNullRef(ref value))
        {
            result = default!;
            return false;
        }

        result = value.Result;
        return true;
    }

    private readonly ISealable _owner;
    private readonly Dictionary<Type, Wrapper> _dictionary;

    protected TypeDictionary(
        ISealable owner,
        TypeDictionary<TValue, TResult>? source = null)
    {
        _owner = owner;
        _dictionary = source is null ? new() : new(source._dictionary);
    }

    protected abstract TResult InitializeValue(TValue? value);

    public TValue? this[Type key]
    {
        get => _dictionary[key].Value;
        set
        {
            ValidateKey(key);
            _owner.ThrowIfReadOnly();
            _dictionary[key] = new Wrapper(value, InitializeValue(value));
        }
    }

    public int Count => _dictionary.Count;
    public bool IsReadOnly => _owner.IsReadOnly;

    public void Add(Type key, TValue? value)
    {
        ValidateKey(key);
        _owner.ThrowIfReadOnly();
        _dictionary[key] = new Wrapper(value, InitializeValue(value));
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

    public bool TryGetValue(Type key, [MaybeNullWhen(false)] out TValue? value)
    {
        if (_dictionary.TryGetValue(key, out var wrapper))
        {
            value = wrapper.Value;
            return true;
        }

        value = default;
        return false;
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
}
