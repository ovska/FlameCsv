namespace FlameCsv.Converters;

internal sealed class CharTextConverter : CsvConverter<char, char>
{
    public static CharTextConverter Instance { get; } = new();

    public override bool TryParse(ReadOnlySpan<char> source, out char value)
    {
        if (source.Length == 1)
        {
            value = source[0];
            return true;
        }

        value = '\0';
        return false;
    }

    public override bool TryFormat(Span<char> destination, char value, out int charsWritten)
    {
        if (!destination.IsEmpty)
        {
            destination[0] = value;
            charsWritten = 1;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}
