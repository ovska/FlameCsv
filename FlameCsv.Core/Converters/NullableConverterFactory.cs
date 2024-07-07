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
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public override CsvConverter<T> Create(Type type, CsvOptions<T> options)
    {
        var structType = type.GetGenericArguments()[0];
        var converterOfT = options.GetConverter(structType);

#if false
        // If the value type has an interface or object converter, just return that converter directly.
        // e.g. a struct that implements IEnumerable<T>
        // this matches the behavior of System.Text.Json
        if (structType.IsValueType && converterOfT.Type is { IsValueType: false })
        {
            if (!RuntimeFeature.IsDynamicCodeSupported)
                throw new NotSupportedException();

#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return CastingConverter.Create(converterOfT.Type, type, converterOfT);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        }
#endif

        return GetParserType(structType).CreateInstance<CsvConverter<T>>(converterOfT, options.GetNullToken(type));
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = Messages.StructFactorySuppressionMessage)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
#pragma warning disable RCS1158 // Static member in generic type should use a type parameter
    internal static Type GetParserType(Type type) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), type);
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter
}
