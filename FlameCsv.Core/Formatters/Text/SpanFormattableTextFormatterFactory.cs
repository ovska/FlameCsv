using FlameCsv.Configuration;
using FlameCsv.Extensions;
using FlameCsv.Writers;

namespace FlameCsv.Formatters.Text;

public sealed class SpanFormattableTextFormatterFactory : ICsvFormatterFactory<char>
{
    public bool CanFormat(Type valueType)
    {
        return valueType.IsAssignableTo(typeof(ISpanFormattable));
    }

    public ICsvFormatter<char> Create(Type valueType, CsvWriterOptions<char> options)
    {
        var nullToken = (options as ICsvNullTokenConfiguration<char>).GetNullTokenOrDefault(valueType);
        var format = (options as ICsvFormatConfiguration<char>).GetFormatOrDefault(valueType);
        var formatProvider = (options as ICsvFormatProviderConfiguration<char>).GetFormatProviderOrDefault(valueType);
        var formatterType = typeof(SpanFormattableTextFormatter<>).MakeGenericType(valueType);

        return formatterType.CreateInstance<ICsvFormatter<char>>(nullToken, formatProvider, format);
    }
}
