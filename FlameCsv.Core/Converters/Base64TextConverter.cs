using System.Buffers.Text;

namespace FlameCsv.Converters;

internal sealed class Base64TextConverter : CsvConverter<char, Memory<byte>>
{
    internal static Base64TextConverter Instance { get; } = new();

    private Base64TextConverter()
    {
    }

    public override bool TryParse(ReadOnlySpan<char> source, out Memory<byte> value)
    {
        if (Base64.IsValid(source, out int decodedLength))
        {
            byte[] array = new byte[decodedLength];

            if (Convert.TryFromBase64Chars(source, array, out int bytesWritten))
            {
                value = new Memory<byte>(array, 0, bytesWritten);
                return true;
            }
        }

        value = default;
        return false;
    }

    public override bool TryFormat(Span<char> destination, Memory<byte> value, out int charsWritten)
    {
        return Convert.TryToBase64Chars(value.Span, destination, out charsWritten);
    }
}
