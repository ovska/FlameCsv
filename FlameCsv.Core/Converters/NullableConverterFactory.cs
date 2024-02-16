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

    public override bool CanConvert(Type type)
    {
        return type.IsValueType
            && type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public override CsvConverter<T> Create(Type type, CsvOptions<T> options)
    {
        var innerType = Nullable.GetUnderlyingType(type)!;
        var inner = options.GetConverter(innerType);
        return GetParserType(innerType).CreateInstance<CsvConverter<T>>(inner, options.GetNullToken(type));
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type type) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), type);
}
