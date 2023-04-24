using FlameCsv.Extensions;

namespace FlameCsv.Parsers;

/// <summary>
/// Factory for <see cref="NullableParser{T,TValue}"/>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
public sealed class NullableParserFactory<T> : ICsvParserFactory<T>
    where T : unmanaged, IEquatable<T>
{
    public bool CanParse(Type resultType)
    {
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public ICsvParser<T> Create(Type resultType, CsvReaderOptions<T> options)
    {
        var innerType = Nullable.GetUnderlyingType(resultType)!;
        var inner = options.GetParser(innerType);
        return typeof(NullableParser<,>)
            .MakeGenericType(typeof(T), innerType)
            .CreateInstance<ICsvParser<T>>(inner, options.GetNullToken(resultType));
    }

    public static NullableParserFactory<T> Instance { get; } = new();
}
