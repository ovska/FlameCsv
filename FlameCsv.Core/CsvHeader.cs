using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
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
    static CsvHeader()
    {
        if (Messages.CachingDisabled)
        {
            HeaderPool = null!;
        }
        else
        {
            HeaderPool = new(minimumSize: 32);
        }

        HotReloadService.RegisterForHotReload(HeaderPool, static state => ((StringPool)state)?.Reset());
    }

    internal static readonly StringPool HeaderPool;

    internal static TResult Parse<T, TState, TResult>(
        CsvOptions<T> options,
        ref CsvFieldsRef<T> record,
        TState state,
        [RequireStaticDelegate] Func<TState, ReadOnlySpan<string>, TResult> callback)
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

        ReadOnlySpan<string> headers = list.AsSpan();

        return callback(state, headers);
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
        // pool headers if we know someone hasn't overridden the default implementation,
        // so multiple different options-types don't write their own intrepretation to the same cache
        if (!Messages.CachingDisabled &&
            !options.IsInherited &&
            options.TryGetChars(value, buffer, out int length))
        {
            return HeaderPool.GetOrAdd(buffer.Slice(0, length));
        }

        return options.GetAsString(value);
    }

    /// <summary>
    /// Returns the header values.
    /// </summary>
    public ReadOnlySpan<string> Values => _header;

    private readonly IEqualityComparer<string> _comparer;
    private readonly string[] _header;
    private readonly int[] _hashCodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeader"/> class.
    /// </summary>
    /// <param name="comparer">Comparer to use</param>
    /// <param name="header">Header values</param>
    public CsvHeader(IEqualityComparer<string> comparer, ReadOnlySpan<string> header)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentOutOfRangeException.ThrowIfZero(header.Length);

        _comparer = comparer;
        _header = header.ToArray();
        _hashCodes = new int[_header.Length];

        for (int i = 0; i < _header.Length; i++)
        {
            _hashCodes[i] = comparer.GetHashCode(_header[i]);
        }
    }

    /// <summary>
    /// Returns the number of header values.
    /// </summary>
    public int Count => _header.Length;

    /// <summary>
    /// Returns <see langword="true"/> if the specified header is present.
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
        ReadOnlySpan<string> values = Values;

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
            // intentionally iterate to avoid exposing the array as an IEnumerable
            foreach (var header in _header) yield return header;
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
