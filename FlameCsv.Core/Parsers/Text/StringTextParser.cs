namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see cref="string"/> and <see cref="char"/> arrays.
/// </summary>
public sealed class StringTextParser :
    ICsvParser<char, string?>,
    ICsvParser<char, char[]>,
    ICsvParser<char, Memory<char>>,
    ICsvParser<char, ReadOnlyMemory<char>>
{
    /// <summary>
    /// Whether empty strings are returned as null.
    /// </summary>
    public bool ReadEmptyAsNull { get; }

    private readonly string? _empty;

    public StringTextParser(bool readEmptyAsNull = false)
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

    public static StringTextParser Instance { get; } = new();

    internal static StringTextParser GetOrCreate(bool readEmptyAsNull)
    {
        return !readEmptyAsNull ? Instance : new(readEmptyAsNull);
    }
}
