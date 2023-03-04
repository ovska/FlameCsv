#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Parsers.Text;

public sealed class SpanParsableTextParserFactory : ICsvParserFactory<char>
{
    /// <summary>
    /// Format providers indexed by type to use instead of <see cref="CultureInfo.InvariantCulture"/>. Null values are allowed.
    /// </summary>
    public IDictionary<Type, IFormatProvider?> FormatProviders { get; } = new Dictionary<Type, IFormatProvider?>();

    public bool CanParse(Type resultType)
        => resultType
            .GetInterfaces()
            .Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISpanParsable<>));

    public ICsvParser<char> Create(Type resultType, CsvReaderOptions<char> options)
    {
        return ActivatorEx.CreateInstance<ICsvParser<char>>(
            typeof(SpanParsableTextParser<>).MakeGenericType(resultType),
            parameters: new object?[] { FormatProviders.GetValueOrDefault(resultType, CultureInfo.InvariantCulture) });
    }
}

public sealed class SpanParsableTextParser<T> : ICsvParser<char, T> where T : ISpanParsable<T>
{
    public IFormatProvider? FormatProvider { get; }

    /// <summary>
    /// Initializes a <see cref="SpanParsableTextParser{T}"/> using the invariant culture.
    /// </summary>
    public SpanParsableTextParser() : this(CultureInfo.InvariantCulture)
    {
    }

    /// <summary>
    /// Initializes a <see cref="SpanParsableTextParser{T}"/> using the specified format provider.
    /// </summary>
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
#endif
