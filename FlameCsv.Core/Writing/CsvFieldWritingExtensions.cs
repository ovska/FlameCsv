namespace FlameCsv.Writing;

/// <summary>
/// Extensions for formatting values into <see cref="CsvFieldWriter{T}"/>
/// </summary>
public static class CsvFieldWritingExtensions
{
    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<char> writer,
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        Span<char> destination = writer.Writer.GetSpan();
        int charsWritten;

        while (!value.TryFormat(destination, out charsWritten, format, formatProvider))
        {
            destination = writer.Writer.GetSpan(destination.Length * 2);
        }

        writer.AdvanceAndHandleQuoting(destination, charsWritten);
    }

    public static void FormatValue<T>(
        ref readonly this CsvFieldWriter<byte> writer,
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? formatProvider = null)
        where T : IUtf8SpanFormattable
    {
        Span<byte> destination = writer.Writer.GetSpan();
        int bytesWritten;

        while (!value.TryFormat(destination, out bytesWritten, format, formatProvider))
        {
            destination = writer.Writer.GetSpan(destination.Length * 2);
        }

        writer.AdvanceAndHandleQuoting(destination, bytesWritten);
    }
}
