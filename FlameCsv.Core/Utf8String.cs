using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String
{
    private readonly string _string;
    private readonly ReadOnlyMemory<byte> _bytes;

    private Utf8String(string? value)
    {
        _string = value ?? "";
        _bytes = Encoding.UTF8.GetBytes(_string);
    }

    public static implicit operator Utf8String(string? value) => new(value);
    public static implicit operator string?(Utf8String? value) => value?._string;
    public static implicit operator ReadOnlyMemory<byte>(Utf8String value) => value._bytes;
}
