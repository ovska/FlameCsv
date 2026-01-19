using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

#pragma warning disable IDE0038 // Use pattern matching
#pragma warning disable RCS1220 // Use pattern matching instead of combination of 'is' operator and cast operator

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
    /// <param name="skipEscaping">Don't quote or escape the value</param>
    /// <remarks>
    /// Does not write a trailing delimiter or newline.<br/>
    /// Null values are written according to <see cref="CsvFieldWriter{T}.Options"/>.
    /// </remarks>
    public static void FormatValue<T>(
        this CsvFieldWriter<char> writer,
        in T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null,
        bool skipEscaping = false
    )
        where T : ISpanFormattable
    {
        // JITed out for value types
        if (value is null)
        {
            writer.WriteNull<T>();
            return;
        }

        Span<char> destination = writer.Writer.GetSpan();
        int charsWritten;

        while (!value.TryFormat(destination, out charsWritten, format, formatProvider))
        {
            destination = writer.Writer.GetSpan(destination.Length * 2);
        }

        if (skipEscaping)
        {
            writer.Writer.Advance(charsWritten);
        }
        else
        {
            writer.EscapeAndAdvance(destination, charsWritten);
        }
    }

    /// <inheritdoc cref="FormatValue{T}(CsvFieldWriter{char},in T,ReadOnlySpan{char},IFormatProvider?,bool)"/>
    public static void FormatValue<T>(
        this CsvFieldWriter<byte> writer,
        in T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null,
        bool skipEscaping = false
    )
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

        // for value types this condition is a runtime constant
        if (value is IUtf8SpanFormattable)
        {
            while (!((IUtf8SpanFormattable)value).TryFormat(destination, out bytesWritten, format, formatProvider))
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

        if (skipEscaping)
        {
            writer.Writer.Advance(bytesWritten);
        }
        else
        {
            writer.EscapeAndAdvance(destination, bytesWritten);
        }
    }

    /// <inheritdoc cref="FormatValue{T}(CsvFieldWriter{char},in T,ReadOnlySpan{char},IFormatProvider?,bool)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FormatValue<T>(
        this CsvFieldWriter<char> writer,
        T? value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null,
        bool skipEscaping = false
    )
        where T : struct, ISpanFormattable
    {
        if (value.HasValue)
        {
            FormatValue(writer, in Nullable.GetValueRefOrDefaultRef(in value), format, formatProvider, skipEscaping);
        }
        else
        {
            writer.WriteNull<T>();
        }
    }

    /// <inheritdoc cref="FormatValue{T}(CsvFieldWriter{char},in T,ReadOnlySpan{char},IFormatProvider?,bool)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FormatValue<T>(
        this CsvFieldWriter<byte> writer,
        T? value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null,
        bool skipEscaping = false
    )
        where T : struct, ISpanFormattable
    {
        if (value.HasValue)
        {
            FormatValue(writer, in Nullable.GetValueRefOrDefaultRef(in value), format, formatProvider, skipEscaping);
        }
        else
        {
            writer.WriteNull<T>();
        }
    }
}
