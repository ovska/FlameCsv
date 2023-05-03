using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers;

/// <summary>
/// Factory for <see cref="NullableParser{T,TValue}"/>
/// </summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
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
        return GetParserType(innerType).CreateInstance<ICsvParser<T>>(inner, options.GetNullToken(resultType));
    }

    public static NullableParserFactory<T> Instance { get; } = new();

    [return: DynamicallyAccessedMembers(Trimming.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Trimming.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(NullableParser<,>).MakeGenericType(typeof(T), resultType);
}
