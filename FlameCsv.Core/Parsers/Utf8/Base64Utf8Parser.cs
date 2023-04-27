using System.Buffers;
using System.Buffers.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parses Base64 fields into byte arrays or its' derivatives.
/// </summary>
public sealed class Base64Utf8Parser :
    ICsvParser<byte, byte[]>,
    ICsvParser<byte, ArraySegment<byte>>,
    ICsvParser<byte, Memory<byte>>,
    ICsvParser<byte, ReadOnlyMemory<byte>>,
    ICsvParserFactory<byte>
{
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

    private static byte[] Decode(ReadOnlySpan<byte> input)
    {
        int maxLength = Base64.GetMaxDecodedFromUtf8Length(input.Length);

        if (Token<byte>.CanStackalloc(maxLength))
        {
            Span<byte> buffer = stackalloc byte[maxLength];
            return DecodeImpl(input, buffer);
        }

        using var spanOwner = SpanOwner<byte>.Allocate(maxLength);
        return DecodeImpl(input, spanOwner.Span);
    }

    private static byte[] DecodeImpl(ReadOnlySpan<byte> input, Span<byte> output)
    {
        var status = Base64.DecodeFromUtf8(input, output, out var bytesConsumed, out var bytesWritten);

        if (status != OperationStatus.Done)
        {
            ThrowHelper.ThrowInvalidOperationException($"Failed to decode base64 data: {status}");
        }

        if (bytesConsumed != input.Length)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Base64 data was partial, decoded ${bytesConsumed} out of {input.Length}");
        }

        return output[..bytesWritten].ToArray();
    }

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        return this;
    }
}
