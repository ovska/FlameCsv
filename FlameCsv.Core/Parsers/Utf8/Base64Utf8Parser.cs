using System.Buffers;
using System.Buffers.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Utf8;

public sealed class Base64Utf8Parser :
    ICsvParser<byte, byte[]>,
    ICsvParser<byte, ArraySegment<byte>>,
    ICsvParser<byte, Memory<byte>>,
    ICsvParser<byte, ReadOnlyMemory<byte>>
{
    internal static readonly Base64Utf8Parser Instance = new();

    public bool TryParse(ReadOnlySpan<byte> span, out byte[] value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ArraySegment<byte> value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out Memory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ReadOnlyMemory<byte> value)
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

    private static byte[] Decode(ReadOnlySpan<byte> span)
    {
        using var spanOwner = SpanOwner<byte>.Allocate(Base64.GetMaxDecodedFromUtf8Length(span.Length));

        var status = Base64.DecodeFromUtf8(span, spanOwner.Span, out var bytesConsumed, out var bytesWritten);

        if (status != OperationStatus.Done)
        {
            ThrowHelper.ThrowInvalidOperationException($"Failed to decode base64 data, status was {status}");
        }

        if (bytesConsumed != span.Length)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Base64 data was partial, decoded ${bytesConsumed} out of {span.Length}");
        }

        return spanOwner.Span.Slice(0, bytesWritten).ToArray();
    }
}
