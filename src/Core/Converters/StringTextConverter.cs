using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class StringTextConverter : CsvConverter<char, string>
{
    public static StringTextConverter Instance { get; } = new();

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        // use String.TryCopyTo to avoid conversion to span
        if (value.TryCopyTo(destination))
        {
            charsWritten = value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        // the ROS<char> ctor is intrinsic, and will return empty string singleton for empty input
        value = new string(source);
        return true;
    }
}
