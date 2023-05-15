using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters;

internal sealed class DateTimeOffsetTextConverter : CsvConverter<char, DateTimeOffset>
{
    private readonly DateTimeStyles _styles;
    private readonly IFormatProvider? _provider;
    private readonly string? _format;

    public DateTimeOffsetTextConverter(CsvTextOptions options)
    {
        _styles = options.DateTimeStyles;
        _provider = options.FormatProvider;
        _format = options.DateTimeFormat;
    }

    public override bool TryFormat(Span<char> destination, DateTimeOffset value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out DateTimeOffset value)
    {
        return _format is null
            ? DateTimeOffset.TryParse(source, _provider, _styles, out value)
            : DateTimeOffset.TryParseExact(source, _format, _provider, _styles, out value);
    }
}
