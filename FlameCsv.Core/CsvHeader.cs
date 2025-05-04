using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Read-only CSV header record.
/// </summary>
[PublicAPI]
public sealed class CsvHeader
{
    // does not need to be cleared on hot-reload, headers are always transcoded the same way
    internal static readonly StringPool? HeaderPool = FlameCsvGlobalOptions.CachingDisabled
        ? null
        : new StringPool(minimumSize: 32);

    internal static ImmutableArray<string> Parse<T>(
        CsvOptions<T> options,
        ref CsvFieldsRef<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (record.FieldCount == 0) CsvFormatException.Throw("CSV header was empty");

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);
        Span<char> charBuffer = stackalloc char[128];

        for (int field = 0; field < record.FieldCount; field++)
        {
            list.Append(Get(options, record[field], charBuffer));
        }

        return [.. list.AsSpan()];
    }

    /// <summary>
    /// Retrieves a string representation of the given value using the provided options.
    /// </summary>
    /// <param name="options">The CSV options used for conversion.</param>
    /// <param name="value">The header field.</param>
    /// <param name="buffer">A buffer to write the characters to.</param>
    /// <returns>
    /// A string representation of the value.
    /// The header values are pooled if they fit in the buffer using <see cref="CsvOptions{T}.TryGetChars"/>,
    /// and the options-type is not inherited.
    /// Otherwise, it is converted to a string using <see cref="CsvOptions{T}.GetAsString"/>.
    /// </returns>
    public static string Get<T>(CsvOptions<T> options, scoped ReadOnlySpan<T> value, scoped Span<char> buffer)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);

        if (HeaderPool is not null && options.TryGetChars(value, buffer, out int length))
        {
            return HeaderPool.GetOrAdd(buffer.Slice(0, length));
        }

        return options.GetAsString(value);
    }

    /// <summary>
    /// Returns the header values.
    /// </summary>
    public ImmutableArray<string> Values { get; }

    private readonly IEqualityComparer<string> _comparer;
    private readonly int[] _hashCodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeader"/> class.
    /// </summary>
    /// <param name="comparer">Comparer to use</param>
    /// <param name="header">Header values</param>
    public CsvHeader(IEqualityComparer<string> comparer, ImmutableArray<string> header)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(ImmutableCollectionsMarshal.AsArray(header), nameof(header));
        ArgumentOutOfRangeException.ThrowIfZero(header.Length);

        _comparer = comparer;
        Values = header;
        _hashCodes = new int[header.Length];

        for (int i = 0; i < header.Length; i++)
        {
            _hashCodes[i] = comparer.GetHashCode(header[i]);
        }
    }

    /// <summary>
    /// Returns the number of header values.
    /// </summary>
    public int Count => Values.Length;

    /// <summary>
    /// Returns <c>true</c> if the specified header is present.
    /// </summary>
    /// <param name="key">Header name</param>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = _comparer.GetHashCode(key);

        foreach (var header in Values)
        {
            if (_comparer.GetHashCode(header) == hash && _comparer.Equals(header, key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to return the index of the specified header.
    /// </summary>
    public bool TryGetValue(string key, out int value)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = _comparer.GetHashCode(key);
        ReadOnlySpan<string> values = Values.AsSpan();

        for (int index = 0; index < values.Length; index++)
        {
            if (_hashCodes[index] == hash && _comparer.Equals(values[index], key))
            {
                value = index;
                return true;
            }
        }

        value = -1;
        return false;
    }
}
