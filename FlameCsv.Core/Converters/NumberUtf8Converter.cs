﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters;

internal sealed class NumberUtf8Converter<TValue, TStyles> : CsvConverter<byte, TValue>
    where TValue : INumberBase<TValue>
    where TStyles : INumberStylesDefaultValue
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;
    private readonly NumberStyles _styles;

    public NumberUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
        _styles = options.GetNumberStyles(typeof(TValue), TStyles.Default);
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int bytesWritten)
    {
        return value.TryFormat(destination, out bytesWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _styles, _provider, out value);
    }
}
