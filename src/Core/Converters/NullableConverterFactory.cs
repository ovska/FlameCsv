using FlameCsv.Extensions;

namespace FlameCsv.Converters;

/// <summary>
/// Factory for <see cref="NullableConverter{T,TValue}"/>
/// </summary>
[RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
internal sealed class NullableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static NullableConverterFactory<T> Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsGenericType
            && !type.IsGenericTypeDefinition
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public override CsvConverter<T> Create(Type type, CsvOptions<T> options)
    {
        var structType = type.GetGenericArguments()[0];
        var converterOfT = options.GetConverter(structType);
        return CreateCore(structType, converterOfT, options);
    }

    internal static CsvConverter<T> CreateCore(Type type, CsvConverter<T> inner, CsvOptions<T> options)
    {
        return typeof(NullableConverter<,>)
            .MakeGenericType(typeof(T), type)
            .CreateInstance<CsvConverter<T>>(inner, options.GetNullObject(type));
    }
}
