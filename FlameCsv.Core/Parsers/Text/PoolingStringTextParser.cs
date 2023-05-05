using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// A parser for strings that uses pooling for the strings. Useful for reducing string allocations when the
/// data has small entropy for string values.
/// </summary>
/// <seealso cref="CommunityToolkit.HighPerformance.Buffers.StringPool"/>
public sealed class PoolingStringTextParser : ParserBase<char, string?>, ICsvParserFactory<char>
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
    public PoolingStringTextParser() : this(StringPool.Shared, false)
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

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);
        Guard.IsNotNull(o.StringPool);
        return o.StringPool == StringPool ? this : new PoolingStringTextParser(o.StringPool, o.ReadEmptyStringsAsNull);
    }

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static PoolingStringTextParser Instance { get; } = new();
}
