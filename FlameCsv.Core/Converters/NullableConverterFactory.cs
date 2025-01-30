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
[RDC(Messages.FactoryMessage), RUF(Messages.FactoryMessage)]
internal sealed class NullableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, IBinaryInteger<T>
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
        var nullToken = options.GetNullToken(type);

#if false
        // if the value type has an interface or object converter, return that converter directly.
        // e.g., a struct that implements IEnumerable<T>
        // this matches the behavior of System.Text.Json
        if (structType.IsValueType && converterOfT.ConvertedType is { IsValueType: false })
        {
            if (!RuntimeFeature.IsDynamicCodeSupported)
                throw new NotSupportedException();

#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return CastingConverter.Create(converterOfT.ConvertedType, type, converterOfT);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        }
#endif

        return CreateCore(structType, converterOfT, nullToken);
    }

    internal static CsvConverter<T> CreateCore(
        Type structType,
        CsvConverter<T> converterOfT,
        ReadOnlyMemory<T> nullToken)
    {
        return
            TryGetEmpty(structType, converterOfT, nullToken) ??
            TryGetString(structType, converterOfT, nullToken) ??
            TryGetArray(structType, converterOfT, nullToken) ??
            GetParserType(structType).CreateInstance<CsvConverter<T>>(converterOfT, nullToken);
    }

    internal static Type GetParserType(Type type) => typeof(NullableConverter<,>).MakeGenericType(typeof(T), type);

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

internal static class TrimmableNullableConverter
{
    public static CsvConverter<T, TValue?> Create<T, TValue>(
        CsvConverter<T, TValue> inner,
        ReadOnlyMemory<T> nullToken)
        where T : unmanaged, IBinaryInteger<T>
        where TValue : struct
    {
        if (nullToken.IsEmpty) return new OptimizedNullEmptyConverter<T, TValue>(inner);

        if (MemoryMarshal.TryGetArray(nullToken, out var array))
            return new OptimizedNullArrayConverter<T, TValue>(inner, array);

        if (typeof(T) == typeof(char))
        {
            var nullTokenChar = Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref nullToken);
            if (MemoryMarshal.TryGetString(nullTokenChar, out var value, out var start, out var length))
            {
                return (CsvConverter<T, TValue?>)(object)new OptimizedNullStringConverter<TValue>(
                    Unsafe.As<CsvConverter<char, TValue>>(inner),
                    value,
                    start,
                    length);
            }
        }

        return new NullableConverter<T, TValue>(inner, nullToken);
    }
}
