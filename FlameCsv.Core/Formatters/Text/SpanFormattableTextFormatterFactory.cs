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

    public bool CanFormat(Type resultType)
    {
        return resultType.IsAssignableTo(typeof(ISpanFormattable));
    }

    public ICsvFormatter<char> Create(Type resultType, object? options)
    {
        var formatterType = typeof(SpanFormattableTextFormatter<>).MakeGenericType(resultType);

        var nullToken = _configuration?.TypeNulls.GetValueOrDefault(resultType);
        var formatProvider = _configuration?.TypeFormatProviders.TryGetValue(resultType, out var fp) ?? false
            ? fp
            : CultureInfo.InvariantCulture;
        var format = _configuration?.TypeFormats.GetValueOrDefault(resultType);

        return ActivatorEx.CreateInstance<ICsvFormatter<char>>(
            formatterType,
            nullToken,
            formatProvider,
            format);
    }
}
