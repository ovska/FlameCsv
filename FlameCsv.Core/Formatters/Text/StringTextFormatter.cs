using FlameCsv.Extensions;
using FlameCsv.Writers;

namespace FlameCsv.Formatters.Text;

public sealed class StringTextFormatter :
    ICsvFormatter<char, string?>,
    ICsvFormatter<char, char[]?>,
    ICsvFormatter<char, ArraySegment<char>>,
    ICsvFormatter<char, Memory<char>>,
    ICsvFormatter<char, ReadOnlyMemory<char>>
{
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

    public bool TryFormat(string? value, Span<char> buffer, out int tokensWritten)
    {
        return (value ?? StringNull).AsSpan().TryWriteTo(buffer, out tokensWritten);
    }

    public bool TryFormat(char[]? value, Span<char> buffer, out int tokensWritten)
    {
        return (value is null ? value.AsSpan() : CharArrayNull.AsSpan()).TryWriteTo(buffer, out tokensWritten);
    }

    public bool TryFormat(ArraySegment<char> value, Span<char> buffer, out int tokensWritten)
    {
        return value.AsSpan().TryWriteTo(buffer, out tokensWritten);
    }

    public bool TryFormat(Memory<char> value, Span<char> buffer, out int tokensWritten)
    {
        return value.Span.TryWriteTo(buffer, out tokensWritten);
    }

    public bool TryFormat(ReadOnlyMemory<char> value, Span<char> buffer, out int tokensWritten)
    {
        return value.Span.TryWriteTo(buffer, out tokensWritten);
    }
}
