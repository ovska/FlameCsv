using System.Buffers;
using System.Buffers.Text;

namespace FlameCsv.Converters;

internal sealed class Base64Utf8Converter : CsvConverter<byte, Memory<byte>>
{
    internal static Base64Utf8Converter Instance { get; } = new();

    private Base64Utf8Converter()
    {
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out Memory<byte> value)
    {
        if (Base64.IsValid(source, out int decodedLength))
        {
            byte[] array = new byte[decodedLength];

            if (Base64.DecodeFromUtf8(source, array, out _, out int bytesWritten) == OperationStatus.Done)
            {
                value = new Memory<byte>(array, 0, bytesWritten);
                return true;
            }
        }

        value = default;
        return false;
    }

    public override bool TryFormat(Span<byte> destination, Memory<byte> value, out int charsWritten)
    {
        return Base64.EncodeToUtf8(value.Span, destination, out _, out charsWritten) switch
        {
            OperationStatus.Done => true,
            OperationStatus.DestinationTooSmall => false,
            var status => throw new NotSupportedException($"Could not encode to base64: {status}"),
        };
    }
}
