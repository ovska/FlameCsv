using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

// note: we don't _need_ to clear HeaderPool on hot-reload as the values are always the same

/// <summary>
/// Read-only CSV header record.
/// </summary>
[PublicAPI]
public sealed class CsvHeader : IEquatable<CsvHeader>
{
    /// <summary>
    /// Contains the string pool for header values.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public static StringPool HeaderPool { get; } = new StringPool(minimumSize: 128);

    /// <summary>
    /// Parses fields as strings from the specified record.
    /// </summary>
    public static ImmutableArray<string> Parse<T>(ref readonly CsvRecordRef<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (record.FieldCount == 0)
        {
            CsvFormatException.Throw("CSV header was empty");
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);
        EnumeratorStack stack = new();
        Span<char> buffer = MemoryMarshal.Cast<byte, char>((Span<byte>)stack);

        for (int field = 0; field < record.FieldCount; field++)
        {
            ReadOnlySpan<T> value = record[field];
            string result = Transcode.TryToChars(value, buffer, out int length)
                ? HeaderPool.GetOrAdd(buffer.Slice(0, length))
                : Transcode.ToString(value);
            list.Append(result);
        }

        return [.. list.AsSpan()];
    }

    /// <summary>
    /// The comparer used to compare header values.
    /// </summary>
    public IEqualityComparer<string> Comparer { get; }

    /// <summary>
    /// Returns the header values.
    /// </summary>
    public ImmutableArray<string> Values { get; }

    private readonly int[] _hashCodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeader"/> class.
    /// </summary>
    /// <param name="comparer">Comparer to use</param>
    /// <param name="header">Header values</param>
    public CsvHeader(IEqualityComparer<string> comparer, ImmutableArray<string> header)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (header.IsDefaultOrEmpty)
            Throw.DefaultOrEmptyImmutableArray(nameof(header));

        _hashCodes = new int[header.Length];

        for (int i = 0; i < header.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(header[i], nameof(header));
            _hashCodes[i] = comparer.GetHashCode(header[i]);
        }

        Comparer = comparer;
        Values = header;
    }

    /// <summary>
    /// Copy constructor for <see cref="CsvHeader"/>.
    /// </summary>
    public CsvHeader(CsvHeader other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Comparer = other.Comparer;
        Values = other.Values;
        _hashCodes = other._hashCodes; // this should be safe as the values are private and immutable
    }

    /// <summary>
    /// Returns <c>true</c> if the specified header is present.
    /// </summary>
    /// <param name="key">Header name</param>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = Comparer.GetHashCode(key);
        int[] hashCodes = _hashCodes;
        ReadOnlySpan<string> values = Values.AsSpan();

        for (int index = 0; index < hashCodes.Length; index++)
        {
            if (hashCodes[index] == hash && Comparer.Equals(values[index], key))
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

        int hash = Comparer.GetHashCode(key);
        ReadOnlySpan<string> values = Values.AsSpan();

        for (int index = 0; index < values.Length; index++)
        {
            if (_hashCodes[index] == hash && Comparer.Equals(values[index], key))
            {
                value = index;
                return true;
            }
        }

        value = -1;
        return false;
    }

    /// <inheritdoc/>
    public bool Equals(CsvHeader? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (!Equals(other.Comparer, Comparer))
            return false;

        return Values.AsSpan().SequenceEqual(other.Values.AsSpan(), Comparer);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CsvHeader);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        // we need to add the hash comparer as well; Ordinal and OrdinalIgnoreCase comparers return same hashes
        // for many inputs, which would break the equality contract
        hash.Add(Comparer.GetHashCode());

        for (int i = 0; i < _hashCodes.Length; i++)
        {
            hash.Add(_hashCodes[i]);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"CsvHeader[{Values.Length}]";
    }
}
