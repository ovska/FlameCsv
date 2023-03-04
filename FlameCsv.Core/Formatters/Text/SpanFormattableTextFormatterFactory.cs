using System.Globalization;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

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

    public ICsvFormatter<char> Create(Type valueType, object? options)
    {
        var formatterType = typeof(SpanFormattableTextFormatter<>).MakeGenericType(valueType);

        var nullToken = _configuration?.TypeNulls.GetValueOrDefault(valueType);
        var formatProvider = _configuration?.TypeFormatProviders.GetValueOrDefault(valueType, CultureInfo.InvariantCulture);
        var format = _configuration?.TypeFormats.GetValueOrDefault(valueType);

        return ActivatorEx.CreateInstance<ICsvFormatter<char>>(
            formatterType,
            parameters: new object?[] { nullToken, formatProvider, format });
    }
}
