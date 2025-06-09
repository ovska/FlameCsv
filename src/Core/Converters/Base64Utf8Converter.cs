using System.Buffers;
using System.Buffers.Text;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class Base64Utf8Converter : CsvConverter<byte, ArraySegment<byte>>
{
    internal static Base64Utf8Converter Instance { get; } = new();

    private Base64Utf8Converter() { }

    public override bool TryParse(ReadOnlySpan<byte> source, out ArraySegment<byte> value)
    {
        if (Base64.IsValid(source, out int decodedLength))
        {
            byte[] array = new byte[decodedLength];

            if (Base64.DecodeFromUtf8(source, array, out _, out int bytesWritten) == OperationStatus.Done)
            {
                value = new ArraySegment<byte>(array, offset: 0, count: bytesWritten);
                return true;
            }
        }

        value = default;
        return false;
    }

    public override bool TryFormat(Span<byte> destination, ArraySegment<byte> value, out int charsWritten)
    {
        return Base64.EncodeToUtf8(value.AsSpan(), destination, out _, out charsWritten) switch
        {
            OperationStatus.Done => true,
            OperationStatus.DestinationTooSmall => false,
            var status => throw new NotSupportedException($"Could not encode to base64: {status}"),
        };
    }

    internal static CastingConverter<byte, ArraySegment<byte>, Memory<byte>> Memory { get; } =
        new(
            Instance,
            convertTo: static x => new Memory<byte>(x.Array, x.Offset, x.Count),
            convertFrom: static x => MemoryMarshal.TryGetArray<byte>(x, out var segment) ? segment : x.ToArray()
        );

    internal static CastingConverter<byte, ArraySegment<byte>, ReadOnlyMemory<byte>> ReadOnlyMemory { get; } =
        new(
            Instance,
            convertTo: static x => new ReadOnlyMemory<byte>(x.Array, x.Offset, x.Count),
            convertFrom: static x => MemoryMarshal.TryGetArray(x, out var segment) ? segment : x.ToArray()
        );

    internal static CastingConverter<byte, ArraySegment<byte>, byte[]> Array { get; } =
        new(
            Instance,
            convertTo: static x => x.Count == x.Array!.Length && x.Offset == 0 ? x.Array : [.. x],
            convertFrom: static x => new ArraySegment<byte>(x)
        );
}
