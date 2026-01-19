using System.Globalization;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;

namespace FlameCsv.Reading;

/// <summary>
/// Extensions for use with the source-generator.
/// </summary>
public static class CsvFieldParsingExtensions
{
    /// <summary>
    /// Attempts to parse a nullable value from the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<T>(
        ReadOnlySpan<char> span,
        IFormatProvider? provider,
        CsvTypeMap.NullValue<char> nullValue,
        out T? result
    )
        where T : struct, ISpanParsable<T>
    {
        if (T.TryParse(span, provider, out T parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return nullValue.IsNull(span);
    }

    /// <summary>
    /// Attempts to parse a nullable value from the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<T>(
        ReadOnlySpan<byte> span,
        IFormatProvider? provider,
        CsvTypeMap.NullValue<byte> nullValue,
        out T? result
    )
        where T : struct, IUtf8SpanParsable<T>
    {
        if (T.TryParse(span, provider, out T parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return nullValue.IsNull(span);
    }

    /// <summary>
    /// Attempts to parse a nullable number from the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<T>(
        ReadOnlySpan<char> span,
        NumberStyles styles,
        IFormatProvider? provider,
        CsvTypeMap.NullValue<char> nullValue,
        out T? result
    )
        where T : struct, INumberBase<T>
    {
        if (T.TryParse(span, styles, provider, out T parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return nullValue.IsNull(span);
    }

    /// <summary>
    /// Attempts to parse a nullable number from the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseNumber<T>(
        ReadOnlySpan<byte> span,
        NumberStyles styles,
        IFormatProvider? provider,
        CsvTypeMap.NullValue<byte> nullValue,
        out T? result
    )
        where T : struct, INumberBase<T>
    {
        if (T.TryParse(span, styles, provider, out T parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return nullValue.IsNull(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseEnum<T>(
        ReadOnlySpan<char> span,
        bool ignoreCase,
        CsvTypeMap.NullValue<char> nullValue,
        out T? result
    )
        where T : struct, Enum
    {
        if (Enum.TryParse(span, ignoreCase, out T parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return nullValue.IsNull(span);
    }
}
