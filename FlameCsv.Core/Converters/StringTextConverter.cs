using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class StringTextConverter : CsvConverter<char, string>
{
    public static readonly StringTextConverter Instance = new();

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        return value.AsSpan().TryCopyTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = source.ToString();
        return true;
    }
}
