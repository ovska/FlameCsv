using System.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parses Base64 columns into byte arrays or its' derivatives.
/// </summary>
public sealed class Base64TextParser :
    ICsvParser<char, byte[]>,
    ICsvParser<char, ArraySegment<byte>>,
    ICsvParser<char, Memory<byte>>,
    ICsvParser<char, ReadOnlyMemory<byte>>
{
    /// <summary>
    /// A thread-safe singleton instance of <see cref="Base64TextParser"/>.
    /// </summary>
    public static Base64TextParser Instance { get; } = new();

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out byte[] value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ArraySegment<byte> value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out Memory<byte> value)
    {
        value = Decode(span);
        return true;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ReadOnlyMemory<byte> value)
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

    private static byte[] Decode(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        int bufferSize = (span.Length + 2) / 3 * 4;

        if (Token<byte>.CanStackalloc(bufferSize))
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

        return ThrowForFailedConvert(span.Length);
    }

    private static byte[] ThrowForFailedConvert(int length)
        => throw new UnreachableException($"Failed to convert Span<char>[{length}] to base64");
}
