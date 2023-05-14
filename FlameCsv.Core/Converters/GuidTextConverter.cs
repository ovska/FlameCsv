using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class GuidTextConverter : CsvConverter<char, Guid>
{
    private readonly string? _format;

    public GuidTextConverter(CsvTextOptions options)
    {
        _format = options.GuidFormat;
    }

    public override bool TryFormat(Span<char> buffer, Guid value, out int charsWritten)
    {
        return value.TryFormat(buffer, out charsWritten, _format);
    }

    public override bool TryParse(ReadOnlySpan<char> field, [MaybeNullWhen(false)] out Guid value)
    {
        return _format is null
            ? Guid.TryParse(field, out value)
            : Guid.TryParseExact(field, _format, out value);
    }
}
