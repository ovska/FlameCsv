using System.Collections.Immutable;
using FlameCsv.Binding;

namespace FlameCsv;

internal readonly struct MaterializerKey : IEquatable<MaterializerKey>
{
    private enum Flags
    {
        None = 0,
        Write = 1 << 0,
        IgnoreUnmatched = 1 << 1,
    }

    private readonly object _target;
    private readonly Flags _flags;
    private readonly bool _ignoreCase;
    private readonly ImmutableArray<string> _headers;

    public MaterializerKey(Type writtenType)
    {
        _target = writtenType;
        _flags = Flags.Write;
        _ignoreCase = false;
        _headers = [];
    }

    public MaterializerKey(CsvTypeMap writtenTypeMap)
    {
        _target = writtenTypeMap;
        _flags = Flags.Write;
        _ignoreCase = false;
        _headers = [];
    }

    public MaterializerKey(bool ignoreCase, CsvTypeMap target, bool ignoreUnmatched, ImmutableArray<string> headers)
    {
        _target = target;
        _flags = (ignoreUnmatched ? Flags.IgnoreUnmatched : Flags.None);
        _ignoreCase = ignoreCase;
        _headers = headers;
    }

    public MaterializerKey(bool ignoreCase, Type target, bool ignoreUnmatched, ImmutableArray<string> headers)
    {
        _target = target;
        _flags = (ignoreUnmatched ? Flags.IgnoreUnmatched : Flags.None);
        _ignoreCase = ignoreCase;
        _headers = headers;
    }

    public bool Equals(MaterializerKey other)
    {
        return _flags == other._flags
            && _target.Equals(other._target)
            && _ignoreCase == other._ignoreCase
            && _headers.AsSpan().SequenceEqual(other._headers.AsSpan(), Comparer);
    }

    public override bool Equals(object? obj) => obj is MaterializerKey ck && Equals(ck);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_target);
        hash.Add(_flags);
        hash.Add(_ignoreCase);

        if (!_headers.IsDefault)
        {
            hash.Add(_headers.Length);

            foreach (var header in _headers)
            {
                hash.Add(header, Comparer);
            }
        }

        return hash.ToHashCode();
    }

    private StringComparer Comparer => _ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
