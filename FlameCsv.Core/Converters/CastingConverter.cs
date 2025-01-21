using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal static class CastingConverter
{
    [RDC("Calls System.Type.MakeGenericType(params Type[])")]
    public static CsvConverter<T> Create<T>(Type innerType, Type outerType, CsvConverter<T> inner)
        where T : unmanaged, IBinaryInteger<T>
    {
        return typeof(CastingConverter<,,>).MakeGenericType(typeof(T), innerType, outerType)
            .CreateInstance<CsvConverter<T>>(inner);
    }
}

internal sealed class CastingConverter<T, TFrom, TTo>(CsvConverter<T, TFrom> inner) : CsvConverter<T, TTo>
    where T : unmanaged, IBinaryInteger<T>
{
    protected internal override bool CanFormatNull => inner.CanFormatNull;

    public override bool TryFormat(Span<T> destination, TTo value, out int charsWritten)
    {
        return inner.TryFormat(destination, (TFrom)(object)value!, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out TTo value)
    {
        if (inner.TryParse(source, out TFrom? value2))
        {
            value = (TTo)(object)value2!;
            return true;
        }

        value = default;
        return false;
    }
}
