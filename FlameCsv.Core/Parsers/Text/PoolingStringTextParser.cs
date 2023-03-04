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

    public PoolingStringTextParser() : this(StringPool.Shared, readEmptyAsNull: false)
    {
    }

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
}
