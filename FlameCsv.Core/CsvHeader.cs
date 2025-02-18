using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Represents the header of a CSV file.
/// </summary>
[PublicAPI]
public sealed class CsvHeader
{
    /// <summary>
    /// Parses headers from the reader.
    /// </summary>
    /// <param name="options">Options instance to get the comparer and transcoding functions from</param>
    /// <param name="record">CSV record reader</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <typeparam name="TRecord">Record field reader</typeparam>
    /// <returns>Parsed CSV header</returns>
    /// <exception cref="CsvFormatException">Thrown when a duplicate header field is found</exception>
    internal static CsvHeader Parse<T, TRecord>(
        CsvOptions<T> options,
        ref TRecord record)
        where T : unmanaged, IBinaryInteger<T>
        where TRecord : ICsvFields<T>, allows ref struct
    {
        IEqualityComparer<string> comparer = options.Comparer;

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);
        Span<char> charBuffer = stackalloc char[128];

        for (int field = 0; field < record.FieldCount; field++)
        {
            list.Append(Get(options, record[field], charBuffer));
        }

        ReadOnlySpan<string> headers = list.AsSpan();

        if (headers.IsEmpty) CsvFormatException.Throw("CSV header was empty");

        for (int i = 0; i < headers.Length; i++)
        {
            for (int j = 0; j < headers.Length; j++)
            {
                if (i != j && comparer.Equals(headers[i], headers[j]))
                {
                    ThrowExceptionForDuplicateHeaderField(i, j, headers);
                }
            }
        }

        return new CsvHeader(comparer, headers);
    }

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

    /// <summary>
    /// Returns the header values.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeader"/> class.
    /// </summary>
    /// <param name="comparer">Comparer to use</param>
    /// <param name="header">Header values</param>
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

    /// <summary>
    /// Returns the number of header values.
    /// </summary>
    public int Count => _header?.Length ?? _scratchLength;

    /// <summary>
    /// Returns <see langword="true"/> if the specified header is present.
    /// </summary>
    /// <param name="key">Header name</param>
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

    /// <summary>
    /// Attempts to return the index of the specified header.
    /// </summary>
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

    /// <summary>
    /// Returns the index of the specified header.
    /// </summary>
    /// <exception cref="ArgumentException">The header is not found.</exception>
    public int this[string key] => TryGetValue(key, out int value) ? value : Throw.Argument_FieldName(key);

    /// <summary>
    /// Returns the header names as an enumerable.
    /// </summary>
    /// <seealso cref="Values"/>
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

    private static void ThrowExceptionForDuplicateHeaderField(
        int index1,
        int index2,
        ReadOnlySpan<string> headers)
    {
        throw new CsvFormatException(
            $"Duplicate header field \"{headers[index1]}\" in at indexes {index1} and {index2}: [{UtilityExtensions.JoinValues(headers)}]");
    }
}
