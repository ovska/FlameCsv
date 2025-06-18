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
    private readonly IEqualityComparer<string> _comparer;
    private readonly ImmutableArray<string> _headers;

    public MaterializerKey(Type writtenType)
    {
        _target = writtenType;
        _flags = Flags.Write;
        _comparer = ReferenceEqualityComparer.Instance;
        _headers = [];
    }

    public MaterializerKey(CsvTypeMap writtenTypeMap)
    {
        _target = writtenTypeMap;
        _flags = Flags.Write;
        _comparer = ReferenceEqualityComparer.Instance;
        _headers = [];
    }

    public MaterializerKey(
        IEqualityComparer<string> comparer,
        CsvTypeMap target,
        bool ignoreUnmatched,
        ImmutableArray<string> headers
    )
    {
        _target = target;
        _flags = (ignoreUnmatched ? Flags.IgnoreUnmatched : Flags.None);
        _comparer = comparer;
        _headers = headers;
    }

    public MaterializerKey(
        IEqualityComparer<string> comparer,
        Type target,
        bool ignoreUnmatched,
        ImmutableArray<string> headers
    )
    {
        _target = target;
        _flags = (ignoreUnmatched ? Flags.IgnoreUnmatched : Flags.None);
        _comparer = comparer;
        _headers = headers;
    }

    public bool Equals(MaterializerKey other)
    {
        return _flags == other._flags
            && _target.Equals(other._target)
            && _comparer.Equals(other._comparer)
            && _headers.AsSpan().SequenceEqual(other._headers.AsSpan(), _comparer);
    }

    public override bool Equals(object? obj) => obj is MaterializerKey ck && Equals(ck);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_target);
        hash.Add(_flags);
        hash.Add(_comparer);

        if (!_headers.IsDefault)
        {
            hash.Add(_headers.Length);

            foreach (var header in _headers)
            {
                hash.Add(header, _comparer);
            }
        }

        return hash.ToHashCode();
    }
}
