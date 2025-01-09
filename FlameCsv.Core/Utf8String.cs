using System.Diagnostics;
using System.Text;

namespace FlameCsv;

/// <summary>
/// UTF-8 string containing the string and byte representations.
/// </summary>
internal sealed class Utf8String : IEquatable<Utf8String>, IEquatable<string>, IEquatable<ReadOnlyMemory<byte>>
{
    public static readonly Utf8String CRLF = new("\r\n", [.."\r\n"u8]);
    public static readonly Utf8String LF = new("\n", [.."\n"u8]);

    private readonly string _string;
    private readonly ReadOnlyMemory<byte> _bytes;

    public Utf8String(string? value)
    {
        _string = value ?? "";
        _bytes = value switch
        {
            null or "" => ReadOnlyMemory<byte>.Empty,
            "\n" => LF._bytes,
            "\r\n" => CRLF._bytes,
            _ => Encoding.UTF8.GetBytes(value),
        };
    }

    public Utf8String(ReadOnlyMemory<byte> value)
    {
        _bytes = value;
        _string = value.Span switch
        {
            [] => "",
            [(byte)'\r', (byte)'\n'] => CRLF._string,
            [(byte)'\n'] => LF._string,
            var s => Encoding.UTF8.GetString(s),
        };
    }

    private Utf8String(string stringValue, byte[] byteValue)
    {
        Debug.Assert(stringValue == Encoding.UTF8.GetString(byteValue));

        _string = stringValue;
        _bytes = byteValue.ToArray();
    }

    public bool Equals(ReadOnlyMemory<byte> other) => _bytes.Span.SequenceEqual(other.Span);
    public bool Equals(string? other) => string.Equals(other, _string, StringComparison.Ordinal);
    public bool Equals(Utf8String? other) => other is not null && Equals(other._string);
    public override string ToString() => _string;
    public override int GetHashCode() => _string?.GetHashCode() ?? 0;
    public override bool Equals(object? obj) => Equals(obj as Utf8String);

    public static implicit operator Utf8String(string? value) => new(value);
    public static implicit operator Utf8String(byte[]? value) => new(value);
    public static implicit operator Utf8String(Memory<byte> value) => new(value);
    public static implicit operator Utf8String(ReadOnlyMemory<byte> value) => new(value);

    public static implicit operator string?(Utf8String? value) => value?._string;
    public static implicit operator ReadOnlyMemory<byte>(Utf8String value) => value._bytes;
    public static implicit operator ReadOnlyMemory<char>(Utf8String value) => value._string.AsMemory();
    public static implicit operator ReadOnlySpan<byte>(Utf8String value) => value._bytes.Span;
    public static implicit operator ReadOnlySpan<char>(Utf8String value) => value._string;
}
