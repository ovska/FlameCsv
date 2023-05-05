using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see cref="string"/> and <see cref="char"/> arrays.
/// </summary>
public sealed class StringTextParser :
    ICsvParser<char, string?>,
    ICsvParser<char, char[]>,
    ICsvParser<char, Memory<char>>,
    ICsvParser<char, ReadOnlyMemory<char>>,
    ICsvParserFactory<char>
{
    /// <summary>
    /// Whether empty strings are returned as null.
    /// </summary>
    public bool ReadEmptyAsNull { get; }

    private readonly string? _empty;

    public StringTextParser() : this(false)
    {
    }

    public StringTextParser(bool readEmptyAsNull)
    {
        ReadEmptyAsNull = readEmptyAsNull;

        if (!ReadEmptyAsNull)
            _empty = "";
    }

    public bool TryParse(ReadOnlySpan<char> span, out string? value)
    {
        value = !span.IsEmpty ? new string(span) : _empty;
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out char[] value)
    {
        value = span.ToArray();
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out Memory<char> value)
    {
        value = span.ToArray();
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out ReadOnlyMemory<char> value)
    {
        value = span.ToArray();
        return true;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(string)
            || resultType == typeof(char[])
            || resultType == typeof(Memory<char>)
            || resultType == typeof(ReadOnlyMemory<char>);
    }

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.StringPool is not null)
        {
            if (o.StringPool == StringPool.Shared && !o.ReadEmptyStringsAsNull)
            {
                return PoolingStringTextParser.Instance;
            }

            return new PoolingStringTextParser(o.StringPool, o.ReadEmptyStringsAsNull);
        }

        return o.ReadEmptyStringsAsNull ? new StringTextParser(true) : this;
    }
}
