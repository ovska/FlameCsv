using System.Buffers;
using System.Buffers.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parses Base64 columns into byte arrays or its' derivatives.
/// </summary>
public sealed class Base64Utf8Parser :
    ICsvParser<byte, byte[]>,
    ICsvParser<byte, ArraySegment<byte>>,
    ICsvParser<byte, Memory<byte>>,
    ICsvParser<byte, ReadOnlyMemory<byte>>
{
    /// <summary>
    /// A thread-safe singleton instance of <see cref="Base64Utf8Parser"/>.
    /// </summary>
    public static Base64Utf8Parser Instance { get; } = new();

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out byte[] value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out ArraySegment<byte> value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out Memory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out ReadOnlyMemory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
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

        return spanOwner.Span[..bytesWritten].ToArray();
    }
}
