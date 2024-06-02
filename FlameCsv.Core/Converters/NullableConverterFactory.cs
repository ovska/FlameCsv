using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

/// <summary>
/// Factory for <see cref="NullableConverter{T,TValue}"/>
/// </summary>
internal sealed class NullableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, IEquatable<T>
{
    private static readonly Type _nullableType = typeof(Nullable<>);

    public static NullableConverterFactory<T> Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == _nullableType;
    }

    public override CsvConverter<T> Create(Type type, CsvOptions<T> options)
    {
        var innerType = type.GetGenericArguments()[0];
        var inner = options.GetConverter(innerType);

        // If the value type has an interface or object converter, just return that converter directly.
        // source: dotnet runtime
        if (innerType.IsValueType && inner.Type is { IsValueType: false })
        {
            return inner;
        }

        return GetParserType(innerType).CreateInstance<CsvConverter<T>>(inner, options.GetNullToken(type));
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = Messages.StructFactorySuppressionMessage)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    internal static Type GetParserType(Type type) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), type);
}
