using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters;

internal sealed class TimeSpanTextConverter : CsvConverter<char, TimeSpan>
{
    private readonly TimeSpanStyles _styles;
    private readonly IFormatProvider? _provider;
    private readonly string? _format;

    public TimeSpanTextConverter(CsvTextOptions options)
    {
        _styles = options.TimeSpanStyles;
        _provider = options.FormatProvider;
        _format = options.TimeSpanFormat;
    }

    public override bool TryFormat(Span<char> buffer, TimeSpan value, out int charsWritten)
    {
        return value.TryFormat(buffer, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<char> field, [MaybeNullWhen(false)] out TimeSpan value)
    {
        return _format is null
            ? TimeSpan.TryParse(field, _provider, out value)
            : TimeSpan.TryParseExact(field, _format, _provider, _styles, out value);
    }
}
