using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

/// <summary>
/// Factory for <see cref="NullableConverter{T,TValue}"/>
/// </summary>
internal sealed class NullableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, IEquatable<T>
{
    public static NullableConverterFactory<T> Instance { get; } = new();

    public override bool CanConvert(Type resultType)
    {
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public override CsvConverter<T> Create(Type resultType, CsvOptions<T> options)
    {
        var innerType = Nullable.GetUnderlyingType(resultType)!;
        var inner = options.GetConverter(innerType);
        return GetParserType(innerType).CreateInstance<CsvConverter<T>>(inner, options.GetNullToken(resultType));
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), resultType);
}
