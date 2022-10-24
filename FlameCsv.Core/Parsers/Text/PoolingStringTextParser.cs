using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// A parser for strings that uses pooling for the strings. Useful for reducing string allocations when the
/// data has small entropy for string values.
/// </summary>
public sealed class PoolingStringTextParser : ParserBase<char, string?>
{
    /// <inheritdoc cref="StringTextParser"/>
    public bool ReadEmptyAsNull { get; }

    private readonly StringPool _stringPool;

    public PoolingStringTextParser(StringPool stringPool, bool readEmptyAsNull = false)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        _stringPool = stringPool;
        ReadEmptyAsNull = readEmptyAsNull;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out string? value)
    {
        value = span.IsEmpty && ReadEmptyAsNull ? null : _stringPool.GetOrAdd(span);
        return true;
    }
}
