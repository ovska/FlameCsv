using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String
{
    public string String { get; }

    public byte[] GetBytes() => _bytes ??= Encoding.UTF8.GetBytes(String);

    private byte[]? _bytes;

    public Utf8String(string? value)
    {
        String = value ?? "";
    }

    public static implicit operator Utf8String(string? value) => new(value);
    public static implicit operator string?(Utf8String? value) => value?.String;
}
