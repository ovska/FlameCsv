using System.Collections;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv;

internal sealed class CsvHeader : IReadOnlyDictionary<string, int>, IEquatable<CsvHeader>
{
    public ReadOnlySpan<string> Values => _header;

    private readonly IEqualityComparer<string> _comparer;
    private readonly string[] _header;
    private FrozenDictionary<string, int>? _dictionary;
    private int _accessCount;

    private FrozenDictionary<string, int>? Dictionary
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_dictionary is not null)
                return _dictionary;

            if (++_accessCount > 4)
            {
                return _dictionary = _header.Index().ToFrozenDictionary(x => x.Item, x => x.Index, _comparer);
            }

            return null;
        }
    }

    public CsvHeader(IEqualityComparer<string> comparer, string[] header)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(header);

        if (header.Length == 0)
            Throw.Argument("Header cannot be empty.", nameof(header));

        _comparer = comparer;
        _header = header;
    }

    IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator()
    {
        return _header.Index().Select(x => new KeyValuePair<string, int>(x.Item, x.Index)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, int>>)this).GetEnumerator();
    }

    public int Count => _header.Length;

    public bool ContainsKey(string key)
    {
        if (Dictionary is { } dict)
            return dict.ContainsKey(key);

        foreach (var header in _header)
        {
            if (_comparer.Equals(header, key))
                return true;
        }

        return false;
    }

    public bool TryGetValue(string key, out int value)
    {
        if (Dictionary is { } dict)
            return dict.TryGetValue(key, out value);

        for (int index = 0; index < _header.Length; index++)
        {
            if (_comparer.Equals(_header[index], key))
            {
                value = index;
                return true;
            }
        }

        value = default;
        return false;
    }

    public int this[string key] => TryGetValue(key, out int value) ? value : Throw.Argument_FieldName(key);

    public IEnumerable<string> Keys => _header;
    IEnumerable<int> IReadOnlyDictionary<string, int>.Values => Enumerable.Range(0, _header.Length);

    public bool Equals(CsvHeader? other)
    {
        return other is not null && _header.AsSpan().SequenceEqual(other._header.AsSpan(), _comparer);
    }

    public override bool Equals(object? obj) => Equals(obj as CsvHeader);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var header in _header)
            hash.Add(header, _comparer);

        return hash.ToHashCode();
    }
}
