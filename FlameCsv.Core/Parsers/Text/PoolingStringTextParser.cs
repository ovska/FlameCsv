using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// A parser for strings that uses pooling for the strings. Useful for reducing string allocations when the
/// data has small entropy for string values.
/// </summary>
/// <seealso cref="CommunityToolkit.HighPerformance.Buffers.StringPool"/>
public sealed class PoolingStringTextParser : ParserBase<char, string?>
{
    /// <inheritdoc cref="StringTextParser"/>
    public bool ReadEmptyAsNull { get; }

    /// <summary>
    /// Pool used by the parser.
    /// </summary>
    public StringPool StringPool { get; }

    public PoolingStringTextParser(StringPool stringPool, bool readEmptyAsNull = false)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        StringPool = stringPool;
        ReadEmptyAsNull = readEmptyAsNull;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out string? value)
    {
        value = span.IsEmpty && ReadEmptyAsNull ? null : StringPool.GetOrAdd(span);
        return true;
    }
}
