using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// A parser for strings that uses pooling for the strings. Useful for reducing string allocations when the
/// data has small entropy for string values.
/// </summary>
/// <seealso cref="CommunityToolkit.HighPerformance.Buffers.StringPool"/>
public sealed class PoolingStringTextParser : ParserBase<char, string?>
{
    /// <inheritdoc cref="StringTextParser.ReadEmptyAsNull"/>
    public bool ReadEmptyAsNull { get; }

    /// <summary>
    /// Pool used by the parser.
    /// </summary>
    public StringPool StringPool { get; }

    private readonly string? _empty;

    /// <summary>
    /// Initializes an instance of <see cref="PoolingStringTextParser"/> using <see cref="StringPool.Shared"/>.
    /// </summary>
    public PoolingStringTextParser(bool readEmptyAsNull = false) : this(StringPool.Shared, readEmptyAsNull)
    {
    }

    /// <summary>
    /// Initializes an instance of <see cref="PoolingStringTextParser"/> using the parameters.
    /// </summary>
    public PoolingStringTextParser(StringPool stringPool, bool readEmptyAsNull = false)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        StringPool = stringPool;
        ReadEmptyAsNull = readEmptyAsNull;

        if (!ReadEmptyAsNull)
            _empty = "";
    }

    public override bool TryParse(ReadOnlySpan<char> span, out string? value)
    {
        value = !span.IsEmpty ? StringPool.GetOrAdd(span) : _empty;
        return true;
    }

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static PoolingStringTextParser Instance { get; } = new();

    internal static PoolingStringTextParser GetOrCreate(StringPool stringPool, bool readEmptyAsNull)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        return ReferenceEquals(stringPool, StringPool.Shared) && !readEmptyAsNull
            ? Instance
            : new(stringPool, readEmptyAsNull);
    }
}
