using FlameCsv.Configuration;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers;

/// <summary>
/// Factory for <see cref="NullableParser{T,TValue}"/>
/// </summary>
public sealed class NullableParserFactory<T> : ICsvParserFactory<T>
    where T : unmanaged, IEquatable<T>
{
    public bool CanParse(Type resultType)
    {
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public ICsvParser<T> Create(Type resultType, CsvReaderOptions<T> options)
    {
        ReadOnlyMemory<T> nullToken = (options as ICsvNullTokenConfiguration<T>).GetNullTokenOrDefault(resultType);

        var innerType = Nullable.GetUnderlyingType(resultType)!;
        var inner = options.GetParser(innerType);
        return typeof(NullableParser<,>)
            .MakeGenericType(typeof(T), innerType)
            .CreateInstance<ICsvParser<T>>(inner, nullToken);
    }

    public static NullableParserFactory<T> Instance { get; } = new();
}
