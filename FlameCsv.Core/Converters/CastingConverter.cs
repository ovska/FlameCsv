using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal static class CastingConverter<T> where T : unmanaged, IEquatable<T>
{
    [SuppressMessage("Trimming", "IL2055:Either the type on which the MakeGenericType is called can't be statically determined, or the type parameters to be used for generic arguments can't be statically determined.", Justification = "<Pending>")]
    public static CsvConverter<T>? TryCreate(CsvConverter<T> fromConverter, Type targetType, CsvOptions<T> options)
    {
        Type? csvConverterType = null;
        Type? current = fromConverter.GetType();

        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(CsvConverter<,>))
            {
                csvConverterType = current;
                break;
            }

            current = current.BaseType;
        }

        if (csvConverterType is null)
            return null;

        return typeof(CastingConverter<,,>).MakeGenericType(
            typeof(T),
            csvConverterType.GetGenericArguments()[1],
            targetType)
            !.CreateInstance<CsvConverter<T>>(fromConverter, options.GetNullToken(targetType));

    }
}

internal sealed class CastingConverter<T, TFrom, TTo> : CsvConverter<T, TTo> where T : unmanaged, IEquatable<T>
{
    private readonly CsvConverter<T, TFrom> _inner;
    private readonly ReadOnlyMemory<T> _null;

    public CastingConverter(CsvConverter<T, TFrom> inner, ReadOnlyMemory<T> nullToken)
    {
        _inner = inner;
        _null = nullToken;
    }

    public CastingConverter(CsvOptions<T> options)
    {
        _inner = options.GetConverter<TFrom>();
        _null = options.GetNullToken(typeof(TTo));
    }

    public override bool TryFormat(Span<T> destination, TTo value, out int charsWritten)
    {
        // JITed out if TTo is value type
        if (value is null)
        {
            if (!_inner.HandleNull)
            {
                return _null.Span.TryWriteTo(destination, out charsWritten);
            }

            return _inner.TryFormat(destination, default!, out charsWritten);
        }

        return _inner.TryFormat(destination, (TFrom)(object)value!, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out TTo value)
    {
        if (_inner.TryParse(source, out TFrom? from))
        {
            value = (TTo)(object)from!;
            return true;
        }

        value = default;
        return false;
    }
}
