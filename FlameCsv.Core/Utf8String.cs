using System.Diagnostics;
using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String
{
    public static Utf8String Empty { get; } = new("", []);
    public static Utf8String CRLF { get; } = new("\r\n", "\r\n"u8);
    public static Utf8String LF { get; } = new("\n", "\n"u8);
    public static Utf8String Space { get; } = new(" ", " "u8);

    private readonly string _string;
    private readonly ReadOnlyMemory<byte> _bytes;

    private Utf8String(string value)
    {
        _string = value;
        _bytes = Encoding.UTF8.GetBytes(value);
    }

    private Utf8String(string stringValue, ReadOnlySpan<byte> byteValue)
    {
        Debug.Assert(stringValue == Encoding.UTF8.GetString(byteValue));

        _string = stringValue;
        _bytes = byteValue.ToArray();
    }

    public static implicit operator Utf8String(string? value)
        => value switch
        {
            null or "" => Empty,
            "\n" => LF,
            "\r\n" => CRLF,
            " " => Space,
            _ => new Utf8String(value),
        };

    public static implicit operator string?(Utf8String? value) => value?._string;
    public static implicit operator ReadOnlyMemory<byte>(Utf8String value) => value._bytes;
}
