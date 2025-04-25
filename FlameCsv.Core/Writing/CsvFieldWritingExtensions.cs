using System.Text;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

/// <summary>
/// Extensions for formatting values into <see cref="CsvFieldWriter{T}"/>
/// </summary>
[PublicAPI]
public static class CsvFieldWritingExtensions
{
    /// <summary>
    /// Formats the value to the writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to format.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="formatProvider">The format provider to use.</param>
    /// <remarks>
    /// Does not write a trailing delimiter.<br/>
    /// Null values are not written, use <see cref="CsvFieldWriter{T}.WriteField{TValue}"/> instead.
    /// </remarks>
    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<char> writer,
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        // JITed out for value types
        if (value is null)
        {
            return;
        }

        Span<char> destination = writer.Writer.GetSpan();
        int charsWritten;

        while (!value.TryFormat(destination, out charsWritten, format, formatProvider))
        {
            destination = writer.Writer.GetSpan(destination.Length * 2);
        }

        writer.EscapeAndAdvanceExternal(destination, charsWritten);
    }

    /// <inheritdoc cref="FormatValue{T}(ref readonly FlameCsv.Writing.CsvFieldWriter{char},T,System.ReadOnlySpan{char},System.IFormatProvider?)"/>
    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<byte> writer,
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        // JITed out for value types
        if (value is null)
        {
            writer.WriteNull<T>();
            return;
        }

        Span<byte> destination = writer.Writer.GetSpan();
        int bytesWritten;

        if (value is IUtf8SpanFormattable formattable)
        {
            while (!formattable.TryFormat(destination, out bytesWritten, format, formatProvider))
            {
                destination = writer.Writer.GetSpan(destination.Length * 2);
            }
        }
        else
        {
            using var vsb = new ValueStringBuilder(stackalloc char[128]);
            vsb.AppendFormatted(value, format, formatProvider);

            while (!Encoding.UTF8.TryGetBytes(vsb.AsSpan(), destination, out bytesWritten))
            {
                destination = writer.Writer.GetSpan(destination.Length * 2);
            }

        }

        writer.EscapeAndAdvanceExternal(destination, bytesWritten);
    }

    /// <inheritdoc cref="FormatValue{T}(ref readonly FlameCsv.Writing.CsvFieldWriter{char},T,System.ReadOnlySpan{char},System.IFormatProvider?)"/>
    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<char> writer,
        T? value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : struct, ISpanFormattable
    {
        if (value is not null)
        {
            FormatValue(in writer, value.Value, format, formatProvider);
        }
    }

    /// <inheritdoc cref="FormatValue{T}(ref readonly FlameCsv.Writing.CsvFieldWriter{char},T,System.ReadOnlySpan{char},System.IFormatProvider?)"/>
    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<byte> writer,
        T? value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : struct, ISpanFormattable
    {
        if (value is not null)
        {
            FormatValue(in writer, value.Value, format, formatProvider);
        }
    }
}
