using System.Buffers.Text;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class Base64TextConverter : CsvConverter<char, ArraySegment<byte>>
{
    internal static Base64TextConverter Instance { get; } = new();

    private Base64TextConverter() { }

    public override bool TryParse(ReadOnlySpan<char> source, out ArraySegment<byte> value)
    {
        if (Base64.IsValid(source, out int decodedLength))
        {
            byte[] array = new byte[decodedLength];

            if (Convert.TryFromBase64Chars(source, array, out int bytesWritten))
            {
                value = new ArraySegment<byte>(array, offset: 0, count: bytesWritten);
                return true;
            }
        }

        value = default;
        return false;
    }

    public override bool TryFormat(Span<char> destination, ArraySegment<byte> value, out int charsWritten)
    {
        return Convert.TryToBase64Chars(value.AsSpan(), destination, out charsWritten);
    }

    internal static CastingConverter<char, ArraySegment<byte>, Memory<byte>> Memory { get; } =
        new(
            Instance,
            convertTo: static x => new Memory<byte>(x.Array, x.Offset, x.Count),
            convertFrom: static x => MemoryMarshal.TryGetArray<byte>(x, out var segment) ? segment : x.ToArray()
        );

    internal static CastingConverter<char, ArraySegment<byte>, ReadOnlyMemory<byte>> ReadOnlyMemory { get; } =
        new(
            Instance,
            convertTo: static x => new ReadOnlyMemory<byte>(x.Array, x.Offset, x.Count),
            convertFrom: static x => MemoryMarshal.TryGetArray(x, out var segment) ? segment : x.ToArray()
        );

    internal static CastingConverter<char, ArraySegment<byte>, byte[]> Array { get; } =
        new(
            Instance,
            convertTo: static x => x.Count == x.Array!.Length && x.Offset == 0 ? x.Array : [.. x],
            convertFrom: static x => new ArraySegment<byte>(x)
        );
}
