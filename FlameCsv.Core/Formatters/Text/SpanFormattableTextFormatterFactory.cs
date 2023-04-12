using System.Globalization;
using FlameCsv.Extensions;
using FlameCsv.Writers;

namespace FlameCsv.Formatters.Text;

public sealed class SpanFormattableTextFormatterFactory : ICsvFormatterFactory<char>
{
    private readonly CsvTextFormatterConfiguration? _configuration;

    public SpanFormattableTextFormatterFactory(
        CsvTextFormatterConfiguration? configuration)
    {
        _configuration = configuration;
    }

    public bool CanFormat(Type valueType)
    {
        return valueType.IsAssignableTo(typeof(ISpanFormattable));
    }

    public ICsvFormatter<char> Create(Type valueType, CsvWriterOptions<char> options)
    {
        var formatterType = typeof(SpanFormattableTextFormatter<>).MakeGenericType(valueType);

        var nullToken = _configuration?.TypeNulls.GetValueOrDefaultEx(valueType);
        var formatProvider = _configuration?.TypeFormatProviders.GetValueOrDefaultEx(valueType, CultureInfo.InvariantCulture);
        var format = _configuration?.TypeFormats.GetValueOrDefaultEx(valueType);
        return formatterType.CreateInstance<ICsvFormatter<char>>(nullToken, formatProvider, format);
    }
}
