using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class GuidTextConverter : CsvConverter<char, Guid>
{
    private readonly string? _format;

    public GuidTextConverter(CsvTextOptions options)
    {
        _format = options.GuidFormat;
    }

    public override bool TryFormat(Span<char> destination, Guid value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out Guid value)
    {
        return _format is null
            ? Guid.TryParse(source, out value)
            : Guid.TryParseExact(source, _format, out value);
    }
}
