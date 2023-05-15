using System.Diagnostics;
using System.Text;

namespace FlameCsv;

/// <summary>
/// Represents an UTF-8 string, represented by a string or bytes.
/// </summary>
public readonly struct Utf8String
{
    internal static readonly Utf8String Null = new("null", "null"u8);
    internal static readonly Utf8String CRLF = new("\r\n", "\r\n"u8);
    internal static readonly Utf8String LF = new("\n", "\n"u8);
    internal static readonly Utf8String Space = new(" ", " "u8);

    private readonly string? _string;
    private readonly ReadOnlyMemory<byte> _bytes;

    public Utf8String(string? value)
    {
        _string = value;
    }

    public Utf8String(ReadOnlyMemory<byte> value)
    {
        _bytes = value;
    }

    private Utf8String(string? stringValue, ReadOnlySpan<byte> byteValue)
    {
        Debug.Assert(
            (stringValue.AsSpan().IsEmpty && byteValue.IsEmpty) ||
            stringValue == Encoding.UTF8.GetString(byteValue));

        _string = stringValue;
        _bytes = byteValue.ToArray();
    }

    public static implicit operator Utf8String(string? value) => new(value);
    public static implicit operator Utf8String(byte[]? value) => new(value);
    public static implicit operator Utf8String(Memory<byte> value) => new(value);
    public static implicit operator Utf8String(ReadOnlyMemory<byte> value) => new(value);

    public static implicit operator string(in Utf8String value)
    {
        if (value._string is not null)
            return value._string;

        if (value._bytes.IsEmpty)
            return "";

        var span = value._bytes.Span;

        if (span.SequenceEqual("\r\n"u8))
            return "\r\n";

        if (span.SequenceEqual("\n"u8))
            return "\n";

        if (span.SequenceEqual("null"u8))
            return "null";

        if (span.SequenceEqual(" "u8))
            return " ";

        return Encoding.UTF8.GetString(span);
    }

    public static implicit operator ReadOnlyMemory<byte>(in Utf8String value)
    {
        return value._string switch
        {
            null => value._bytes,
            "" => default,
            " " => Space._bytes,
            _ when !value._bytes.IsEmpty => value._bytes,
            "\r\n" => CRLF._bytes,
            "\n" => LF._bytes,
            "null" => Null._bytes,
            _ => Encoding.UTF8.GetBytes(value._string)
        };
    }
}
