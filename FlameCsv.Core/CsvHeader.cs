using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv;

internal sealed class CsvHeader
{
    /// <summary>
    /// Retrieves a string representation of the given value using the provided options.
    /// </summary>
    /// <param name="options">The CSV options used for conversion.</param>
    /// <param name="value">The header field.</param>
    /// <param name="buffer">A buffer to write the characters to.</param>
    /// <returns>
    /// A string representation of the value.
    /// The header values are pooled if they fit in the buffer using <see cref="CsvOptions{T}.TryGetChars"/>.
    /// Otherwise, it is converted to a string using <see cref="CsvOptions{T}.GetAsString"/>.
    /// </returns>
    public static string Get<T>(CsvOptions<T> options, scoped ReadOnlySpan<T> value, scoped Span<char> buffer)
        where T : unmanaged, IBinaryInteger<T>
    {
        return options.TryGetChars(value, buffer, out int length)
            ? HeaderPool.GetOrAdd(buffer.Slice(0, length))
            : options.GetAsString(value);
    }

    internal static readonly StringPool HeaderPool = new(minimumSize: 32);

    public ReadOnlySpan<string> Values => _header ?? ((ReadOnlySpan<string>)_scratch!).Slice(0, _scratchLength);

    private readonly IEqualityComparer<string> _comparer;
    private FrozenDictionary<string, int>? _dictionary;
    private int _accessCount;

    private readonly string[]? _header;

    private readonly StringScratch _scratch;
    private readonly int _scratchLength;

    private FrozenDictionary<string, int>? Dictionary
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_dictionary is not null)
                return _dictionary;

            // TODO: profile
            if (++_accessCount > 4)
            {
                return InitDictionary();
            }

            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private FrozenDictionary<string, int> InitDictionary()
    {
        Dictionary<string, int> result = new(Count, _comparer);

        ReadOnlySpan<string> values = Values;

        for (int index = 0; index < values.Length; index++)
        {
            result[values[index]] = index;
        }

        return _dictionary ??= result.ToFrozenDictionary(_comparer);
    }

    public CsvHeader(IEqualityComparer<string> comparer, ReadOnlySpan<string> header)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (header.Length == 0)
            Throw.Argument("Header cannot be empty.", nameof(header));

        _comparer = comparer;

        if (header.Length <= StringScratch.MaxLength)
        {
            _scratch = default;
            header.CopyTo(_scratch!);
            _scratchLength = header.Length;
        }
        else
        {
            _header = header.ToArray();
        }
    }

    public int Count => _header?.Length ?? _scratchLength;

    public bool ContainsKey(string key)
    {
        if (Dictionary is { } dict)
            return dict.ContainsKey(key);

        foreach (var header in Values)
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

        ReadOnlySpan<string> values = Values;
        for (int index = 0; index < values.Length; index++)
        {
            if (_comparer.Equals(values[index], key))
            {
                value = index;
                return true;
            }
        }

        value = 0;
        return false;
    }

    public int this[string key] => TryGetValue(key, out int value) ? value : Throw.Argument_FieldName(key);

    public IEnumerable<string> HeaderNames
    {
        get
        {
            if (_header is not null)
            {
                foreach (var header in _header)
                {
                    yield return header;
                }
            }
            else
            {
                for (int index = 0; index < _scratchLength; index++)
                {
                    yield return _scratch[index]!;
                }
            }

        }
    }
}
