using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Utf8;

public sealed class StringUtf8Formatter :
    ICsvFormatter<byte, string?>,
    ICsvFormatter<byte, char[]?>,
    ICsvFormatter<byte, ArraySegment<char>>,
    ICsvFormatter<byte, Memory<char>>,
    ICsvFormatter<byte, ReadOnlyMemory<char>>
{
    /// <summary>
    /// Token to write if the value is null.
    /// </summary>
    public ReadOnlyMemory<byte> Null { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="nullToken"></param>
    public StringUtf8Formatter(ReadOnlyMemory<byte> nullToken = default)
    {
        Null = nullToken;
    }

    public bool TryFormat(string? value, Span<byte> destination, out int tokensWritten)
    {
        if (value is null)
            return Null.Span.TryWriteTo(destination, out tokensWritten);

        return Core(value.AsSpan(), destination, out tokensWritten);
    }

    public bool TryFormat(char[]? value, Span<byte> destination, out int tokensWritten)
    {
        if (value is null)
            return Null.Span.TryWriteTo(destination, out tokensWritten);

        return Core(value.AsSpan(), destination, out tokensWritten);
    }

    public bool TryFormat(ArraySegment<char> value, Span<byte> destination, out int tokensWritten)
    {
        return Core(value.AsSpan(), destination, out tokensWritten);
    }

    public bool TryFormat(Memory<char> value, Span<byte> destination, out int tokensWritten)
    {
        return Core(value.Span, destination, out tokensWritten);
    }

    public bool TryFormat(ReadOnlyMemory<char> value, Span<byte> destination, out int tokensWritten)
    {
        return Core(value.Span, destination, out tokensWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Core(ReadOnlySpan<char> value, Span<byte> buffer, out int tokensWritten)
    {
        if (value.IsEmpty)
        {
            tokensWritten = 0;
            return true;
        }

        // Try with less expensive check first
        if (buffer.Length >= Encoding.UTF8.GetMaxByteCount(value.Length)
            || buffer.Length >= Encoding.UTF8.GetByteCount(value))
        {
            tokensWritten = Encoding.UTF8.GetBytes(value, buffer);
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    public bool CanFormat(Type resultType)
    {
        return resultType == typeof(string)
            || resultType == typeof(char[])
            || resultType == typeof(ArraySegment<char>)
            || resultType == typeof(Memory<char>)
            || resultType == typeof(ReadOnlyMemory<char>);
    }
}
