using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class StringTextConverter : CsvConverter<char, string>
{
    public override bool HandleNull => true;

    private readonly string? _null;

    public StringTextConverter(CsvTextOptions options)
    {
        _null = options.NullTokens.TryGetValue(typeof(string), out var value) ? value : null;
    }

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        if (value is null)
            return _null.AsSpan().TryWriteTo(destination, out charsWritten);

        return value.AsSpan().TryWriteTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = new string(source);
        return true;
    }
}
