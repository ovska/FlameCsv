﻿using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal static class CastingConverter
{
    [RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
    public static CsvConverter<T> Create<T>(Type innerType, Type outerType, CsvConverter<T> inner) where T: unmanaged, IEquatable<T>
    {
        return typeof(CastingConverter<,,>).MakeGenericType(typeof(T), innerType, outerType).CreateInstance<CsvConverter<T>>(inner);
    }
}

internal sealed class CastingConverter<T, TFrom, TTo> : CsvConverter<T, TTo> where T : unmanaged, IEquatable<T>
    where TFrom : TTo
{
    public override bool HandleNull => _inner.HandleNull;

    private readonly CsvConverter<T, TFrom> _inner;

    public CastingConverter(CsvConverter<T, TFrom> inner)
    {
        _inner = inner;
    }

    public override bool TryFormat(Span<T> destination, TTo value, out int charsWritten)
    {
        return _inner.TryFormat(destination, (TFrom)value!, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out TTo value)
    {
        if (_inner.TryParse(source, out TFrom? value2))
        {
            value = value2;
            return true;
        }

        value = default;
        return false;
    }
}