using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class SpanParsableTextParserFactory : ICsvParserFactory<char>
{
    public bool CanParse(Type resultType)
        => resultType
            .GetInterfaces()
            .Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpanParsable<>));

    public ICsvParser<char> Create(Type resultType, CsvReaderOptions<char> options)
    {
        return (ICsvParser<char>)typeof(SpanParsableTextParser<>)
            .MakeGenericType(resultType)
            .GetProperty("Default")!
            .GetValue(null)!;
    }
}

public sealed class SpanParsableTextParser<T> : ICsvParser<char, T>
    where T : ISpanParsable<T>
{
    /// <summary>
    /// Parser with the default format provider <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public static SpanParsableTextParser<T> Default { get; } = new(CultureInfo.InvariantCulture);

    public IFormatProvider? FormatProvider { get; }

    public SpanParsableTextParser(IFormatProvider? formatProvider)
    {
        FormatProvider = formatProvider;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(T);
    }

    public bool TryParse(ReadOnlySpan<char> span, [MaybeNullWhen(false)] out T value)
    {
        return T.TryParse(span, FormatProvider, out value);
    }
}
