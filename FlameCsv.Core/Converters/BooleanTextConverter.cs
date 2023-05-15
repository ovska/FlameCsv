using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class BooleanTextConverter : CsvConverter<char, bool>
{
    public static BooleanTextConverter Instance { get; } = new();

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        return (value ? "true" : "false").AsSpan().TryWriteTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out bool value)
    {
        return bool.TryParse(source, out value);
    }
}
