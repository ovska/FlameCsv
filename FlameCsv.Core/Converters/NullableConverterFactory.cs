using System.Runtime.InteropServices;
using FlameCsv.Extensions;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv.Converters;

/// <summary>
/// Factory for <see cref="NullableConverter{T,TValue}"/>
/// </summary>
internal sealed partial class NullableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static NullableConverterFactory<T> Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    public override CsvConverter<T> Create(Type type, CsvOptions<T> options)
    {
        var structType = type.GetGenericArguments()[0];
        var converterOfT = options.GetConverter(structType);
        var nullToken = options.GetNullToken(type);

#if false
        // if the value type has an interface or object converter, return that converter directly.
        // e.g., a struct that implements IEnumerable<T>
        // this matches the behavior of System.Text.Json
        if (structType.IsValueType && converterOfT.ConvertedType is { IsValueType: false })
        {
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return CastingConverter.Create(converterOfT.ConvertedType, type, converterOfT);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        }
#endif

        return CreateCore(structType, converterOfT, nullToken);
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    internal static CsvConverter<T> CreateCore(
        Type structType,
        CsvConverter<T> converterOfT,
        ReadOnlyMemory<T> nullToken)
    {
        return
            TryGetEmpty(structType, converterOfT, nullToken) ??
            TryGetKnownLength(structType, converterOfT, nullToken) ??
            TryGetString(structType, converterOfT, nullToken) ??
            TryGetArray(structType, converterOfT, nullToken) ??
            GetParserType(structType).CreateInstance<CsvConverter<T>>(converterOfT, nullToken);
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    internal static Type GetParserType(Type type) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), type);

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    private static CsvConverter<T>? TryGetEmpty(
        Type structType,
        CsvConverter<T> converter,
        ReadOnlyMemory<T> nullTokenT)
    {
        if (nullTokenT.IsEmpty)
        {
            return typeof(OptimizedNullEmptyConverter<,>)
                .MakeGenericType(typeof(T), structType)
                .CreateInstance<CsvConverter<T>>(converter);
        }

        return null;
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    private static CsvConverter<T>? TryGetKnownLength(
        Type structType,
        CsvConverter<T> converter,
        ReadOnlyMemory<T> nullTokenT)
    {
        if (nullTokenT.Length <= Container<T>.MaxLength)
        {
            return typeof(OptimizedKnownLengthConverter<,>)
                .MakeGenericType(typeof(T), structType)
                .CreateInstance<CsvConverter<T>>(converter, nullTokenT);
        }

        return null;
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    private static CsvConverter<T>? TryGetArray(
        Type structType,
        CsvConverter<T> converter,
        ReadOnlyMemory<T> nullTokenT)
    {
        if (MemoryMarshal.TryGetArray(nullTokenT, out var array))
        {
            return typeof(OptimizedNullArrayConverter<,>)
                .MakeGenericType(typeof(T), structType)
                .CreateInstance<CsvConverter<T>>(converter, array);
        }

        return null;
    }

    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    private static CsvConverter<T>? TryGetString(
        Type structType,
        CsvConverter<T> converter,
        ReadOnlyMemory<T> nullTokenT)
    {
        if (typeof(T) != typeof(char)) return null;

        var nullToken = Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref nullTokenT);

        if (MemoryMarshal.TryGetString(nullToken, out var value, out var start, out var length))
        {
            return typeof(OptimizedNullStringConverter<>)
                .MakeGenericType(structType)
                .CreateInstance<CsvConverter<T>>(converter, value, start, length);
        }

        return null;
    }
}
