using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Reading;
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
    /// A shared string pool for unnormalized header values.
    /// </summary>
    [CLSCompliant(false)]
    public static StringPool HeaderPool { get; } = new();

    /// <summary>
    /// Parses fields as strings from the specified record.
    /// </summary>
    public static ImmutableArray<string> Parse<T>(CsvRecordRef<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (record._owner is null)
            Throw.Argument_DefaultStruct(typeof(CsvRecordRef<T>), nameof(record));

        string[] result = new string[record.FieldCount];

        if (typeof(T) == typeof(char))
        {
            ref var rec = ref Unsafe.As<CsvRecordRef<T>, CsvRecordRef<char>>(ref record);
            CsvOptions<char> options = rec._owner.Options;

            for (int i = 0; i < rec.FieldCount; i++)
            {
                ReadOnlySpan<char> field = rec[i];
                result[i] =
                    options.NormalizeHeader is not null ? options.NormalizeHeader(field)
                    : field.Length > (EnumeratorStack.Length / sizeof(char)) ? new string(field)
                    : HeaderPool.GetOrAdd(field);
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(result);
        }

        if (typeof(T) == typeof(byte))
        {
            ref var rec = ref Unsafe.As<CsvRecordRef<T>, CsvRecordRef<byte>>(ref record);
            CsvOptions<byte> options = rec._owner.Options;

            EnumeratorStack stack = default;
            Span<char> buffer = MemoryMarshal.Cast<byte, char>((Span<byte>)stack);

            for (int i = 0; i < rec.FieldCount; i++)
            {
                ReadOnlySpan<byte> field = rec[i];

                if (Encoding.UTF8.TryGetChars(field, buffer, out int charCount))
                {
                    Span<char> slice = buffer.Slice(0, charCount);
                    result[i] = options.NormalizeHeader?.Invoke(slice) ?? HeaderPool.GetOrAdd(slice);
                }
                else
                {
                    string value = Encoding.UTF8.GetString(field);
                    result[i] = options.NormalizeHeader?.Invoke(value) ?? value;
                }
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(result);
        }

        throw Token<T>.NotSupported;
    }

    /// <summary>
    /// Returns the header values.
    /// </summary>
    public ImmutableArray<string> Values { get; }

    private readonly int[] _hashCodes;
    private readonly bool _ignoreCase;

    private StringComparison Comparison => _ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeader"/> class.
    /// </summary>
    /// <param name="ignoreCase">Whether to ignore case when comparing headers</param>
    /// <param name="header">Header values</param>
    public CsvHeader(bool ignoreCase, ImmutableArray<string> header)
    {
        if (header.IsDefaultOrEmpty)
            Throw.DefaultOrEmptyImmutableArray(nameof(header));

        _hashCodes = new int[header.Length];
        _ignoreCase = ignoreCase;

        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] is null)
            {
                Throw.ArgumentNull($"header[{i}]");
            }

            _hashCodes[i] = header[i].GetHashCode(Comparison);
        }

        Values = header;
    }

    /// <summary>
    /// Copy constructor for <see cref="CsvHeader"/>.
    /// </summary>
    public CsvHeader(CsvHeader other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Values = other.Values;
        _ignoreCase = other._ignoreCase;
        _hashCodes = other._hashCodes; // this should be safe as the values are private and immutable
    }

    /// <summary>
    /// Returns <c>true</c> if the specified header is present.
    /// </summary>
    /// <param name="key">Header name</param>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = key.GetHashCode(Comparison);
        int[] hashCodes = _hashCodes;
        ReadOnlySpan<string> values = Values.AsSpan();

        for (int index = 0; index < hashCodes.Length; index++)
        {
            if (hashCodes[index] == hash && values[index].Equals(key, Comparison))
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

        int hash = key.GetHashCode(Comparison);
        ReadOnlySpan<string> values = Values.AsSpan();

        for (int index = 0; index < values.Length; index++)
        {
            if (_hashCodes[index] == hash && values[index].Equals(key, Comparison))
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

        return _ignoreCase == other._ignoreCase
            && Values.AsSpan().SequenceEqual(other.Values.AsSpan(), StringComparer.FromComparison(Comparison));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CsvHeader);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(_ignoreCase ? 1 : 0);

        // we need to add the hash comparer as well; Ordinal and OrdinalIgnoreCase comparers return same hashes
        // for many inputs, which would break the equality contract
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
