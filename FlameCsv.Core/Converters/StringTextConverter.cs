using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class StringTextConverter : CsvConverter<char, string>
{
    public override bool TryFormat(Span<char> buffer, string value, out int charsWritten)
    {
        return value.AsSpan().TryWriteTo(buffer, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> field, [MaybeNullWhen(false)] out string value)
    {
        value = new string(field);
        return true;
    }
}
