using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parses Base64 columns into byte arrays or its' derivatives.
/// </summary>
public class Base64TextParser :
    ICsvParser<char, byte[]>,
    ICsvParser<char, ArraySegment<byte>>,
    ICsvParser<char, Memory<byte>>,
    ICsvParser<char, ReadOnlyMemory<byte>>
{
    public bool TryParse(ReadOnlySpan<char> span, out byte[] value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out ArraySegment<byte> value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out Memory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<char> span, out ReadOnlyMemory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(byte[])
            || resultType == typeof(ArraySegment<byte>)
            || resultType == typeof(Memory<byte>)
            || resultType == typeof(ReadOnlyMemory<byte>);
    }

    private static byte[] Decode(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        int bufferSize = (span.Length + 2) / 3 * 4;

        if (bufferSize <= 256)
        {
            Span<byte> buffer = stackalloc byte[bufferSize];

            if (Convert.TryFromBase64Chars(span, buffer, out var bytesWritten))
                return buffer[..bytesWritten].ToArray();
        }
        else
        {
            using SpanOwner<byte> spanOwner = SpanOwner<byte>.Allocate(bufferSize);

            if (Convert.TryFromBase64Chars(span, spanOwner.Span, out var bytesWritten))
                return spanOwner.Span[..bytesWritten].ToArray();
        }

        return ThrowHelper.ThrowInvalidOperationException<byte[]>(
            $"Failed to convert span<char>[{span.Length}] to base64");
    }
}
