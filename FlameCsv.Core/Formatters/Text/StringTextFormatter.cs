using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Text;

public sealed class StringTextFormatter :
    ICsvFormatter<char, string?>,
    ICsvFormatter<char, char[]?>,
    ICsvFormatter<char, ArraySegment<char>>,
    ICsvFormatter<char, Memory<char>>,
    ICsvFormatter<char, ReadOnlyMemory<char>>
{
    internal static StringTextFormatter Instance { get; } = new();

    /// <summary>
    /// Token to write if the string value is null. Empty string and null are equivalent.
    /// </summary>
    public string? StringNull { get; }

    /// <summary>
    /// Token to write if the char[] value is null. Empty string and null are equivalent.
    /// </summary>
    public string? CharArrayNull { get; }

    public StringTextFormatter(
        string? stringNullToken = null,
        string? charArrayNullToken = null)
    {
        StringNull = stringNullToken;
        CharArrayNull = charArrayNullToken;
    }

    public bool TryFormat(string? value, Span<char> destination, out int tokensWritten)
    {
        return (value ?? StringNull).AsSpan().TryWriteTo(destination, out tokensWritten);
    }

    public bool TryFormat(char[]? value, Span<char> destination, out int tokensWritten)
    {
        return (value is null ? value.AsSpan() : CharArrayNull.AsSpan()).TryWriteTo(destination, out tokensWritten);
    }

    public bool TryFormat(ArraySegment<char> value, Span<char> destination, out int tokensWritten)
    {
        return value.AsSpan().TryWriteTo(destination, out tokensWritten);
    }

    public bool TryFormat(Memory<char> value, Span<char> destination, out int tokensWritten)
    {
        return value.Span.TryWriteTo(destination, out tokensWritten);
    }

    public bool TryFormat(ReadOnlyMemory<char> value, Span<char> destination, out int tokensWritten)
    {
        return value.Span.TryWriteTo(destination, out tokensWritten);
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
