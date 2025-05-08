using System.ComponentModel;
using FlameCsv.Converters.Formattable;
using JetBrains.Annotations;

namespace FlameCsv.Converters;

/// <summary>
/// Provides extensions to create converters for the source generator.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ConverterCreationExtensions
{
    /// <summary>
    /// Creates a converter for a type that implements <see cref="ISpanParsable{T}"/> and <see cref="ISpanFormattable"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TValue> CreateUtf8Parsable<TValue>(CsvOptions<byte> options)
        where TValue : IUtf8SpanParsable<TValue>, ISpanFormattable
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SpanUtf8ParsableConverter<TValue>(options);
    }

    /// <summary>
    /// Creates a UTF-8 converter for a type that implements <see cref="ISpanParsable{T}"/> and <see cref="IUtf8SpanFormattable"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TValue> CreateUtf8Formattable<TValue>(CsvOptions<byte> options)
        where TValue : ISpanParsable<TValue>, IUtf8SpanFormattable
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SpanUtf8FormattableConverter<TValue>(options);
    }

    /// <summary>
    /// Creates a UTF-8 converter for a type that implements <see cref="IUtf8SpanParsable{T}"/> and <see cref="IUtf8SpanFormattable"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TValue> CreateUtf8<TValue>(CsvOptions<byte> options)
        where TValue : IUtf8SpanParsable<TValue>, IUtf8SpanFormattable
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SpanUtf8Converter<TValue>(options);
    }

    /// <summary>
    /// Creates a UTF-8 converter for a type that implements <see cref="ISpanParsable{T}"/> and <see cref="ISpanFormattable"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TValue> CreateUtf8Transcoded<TValue>(CsvOptions<byte> options)
        where TValue : ISpanParsable<TValue>, ISpanFormattable
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SpanUtf8TranscodingConverter<TValue>(options);
    }

    /// <summary>
    /// Creates a UTF-16 converter for a type that implements <see cref="ISpanParsable{T}"/> and <see cref="ISpanFormattable"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TValue> CreateUtf16<TValue>(CsvOptions<char> options)
        where TValue : ISpanParsable<TValue>, ISpanFormattable
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SpanTextConverter<TValue>(options);
    }
}
