using System.Buffers.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    public static readonly BooleanUtf8Converter Instance = new BooleanUtf8Converter();

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        return (value ? "true"u8 : "false"u8).TryWriteTo(destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed)
            && bytesConsumed == source.Length;
    }
}
