namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see cref="string"/> and <see cref="char"/> arrays.
/// </summary>
public sealed class StringTextParser :
    ICsvParser<char, string?>,
    ICsvParser<char, char[]>
{
    /// <summary>
    /// Whether empty strings are returned as null.
    /// </summary>
    public bool ReadEmptyAsNull { get; }

    public StringTextParser(bool readEmptyAsNull = false)
    {
        ReadEmptyAsNull = readEmptyAsNull;
    }

    public bool TryParse(ReadOnlySpan<char> span, out string? value)
    {
        if (span.IsEmpty)
        {
            value = ReadEmptyAsNull ? null : "";
        }
        else
        {
            value = new string(span);
        }

        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out char[] value)
    {
        value = span.ToArray();
        return true;
    }

    public bool CanParse(Type resultType) => resultType == typeof(string) || resultType == typeof(char[]);
}
