﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters;

internal sealed class IntegerNumberUtf8Converter<TValue>(CsvOptions<byte> options)
    : BaseNumberUtf8Converter<TValue>(options)
    where TValue : IBinaryInteger<TValue>
{
    protected override NumberStyles Styles { get; } = options.GetNumberStyles(typeof(TValue), NumberStyles.Integer);
}

internal sealed class FloatNumberUtf8Converter<TValue>(CsvOptions<byte> options)
    : BaseNumberUtf8Converter<TValue>(options)
    where TValue : IFloatingPoint<TValue>
{
    protected override NumberStyles Styles { get; } = options.GetNumberStyles(typeof(TValue), NumberStyles.Float);
}

internal abstract class BaseNumberUtf8Converter<TValue> : CsvConverter<byte, TValue> where TValue : INumberBase<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    protected BaseNumberUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    protected abstract NumberStyles Styles { get; }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, Styles, _provider, out value);
    }
}
