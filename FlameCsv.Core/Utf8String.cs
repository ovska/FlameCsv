using System.Diagnostics;
using System.Text;
using System.Text.Unicode;
using CommunityToolkit.Diagnostics;

namespace FlameCsv;

/// <summary>
/// Represents an UTF-8 string, represented by a string or bytes.
/// </summary>
internal readonly struct Utf8String : IEquatable<Utf8String>, IEquatable<string>, IEquatable<ReadOnlyMemory<byte>>
{
    public static readonly Utf8String Null = new("null", "null"u8);
    public static readonly Utf8String CRLF = new("\r\n", "\r\n"u8);
    public static readonly Utf8String LF = new("\n", "\n"u8);
    public static readonly Utf8String Space = new(" ", " "u8);

    private readonly string? _string;
    private readonly ReadOnlyMemory<byte> _bytes;

    public bool IsEmpty => string.IsNullOrEmpty(_string);

    public Utf8String(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            this = default;
        }
        else if (Null.Equals(value))
        {
            this = Null;
        }
        else if (CRLF.Equals(value))
        {
            this = CRLF;
        }
        else if (LF.Equals(value))
        {
            this = LF;
        }
        else if (Space.Equals(value))
        {
            this = Space;
        }
        else
        {
            _string = value;
            _bytes = Encoding.UTF8.GetBytes(_string);
        }
    }

    public Utf8String(ReadOnlyMemory<byte> value)
    {
        if (value.IsEmpty)
        {
            this = default;
        }
        else if (Null.Equals(value))
        {
            this = Null;
        }
        else if (CRLF.Equals(value))
        {
            this = CRLF;
        }
        else if (LF.Equals(value))
        {
            this = LF;
        }
        else if (Space.Equals(value))
        {
            this = Space;
        }
        else
        {
            if (!Utf8.IsValid(value.Span))
                ThrowHelper.ThrowArgumentException(nameof(value), "Bytes were not well-formed UTF8");

            _string = Encoding.UTF8.GetString(value.Span);
            _bytes = value;
        }
    }

    private Utf8String(string stringValue, ReadOnlySpan<byte> byteValue)
    {
        Debug.Assert(stringValue == Encoding.UTF8.GetString(byteValue));

        _string = stringValue;
        _bytes = byteValue.ToArray();
    }

    public bool Equals(ReadOnlyMemory<byte> other) => _bytes.Span.SequenceEqual(other.Span);
    public bool Equals(string? other) => string.Equals(other, _string ?? "", StringComparison.Ordinal);
    public bool Equals(Utf8String other) => Equals(other._string ?? "");

    public static implicit operator Utf8String(string? value) => new(value);
    public static implicit operator Utf8String(byte[]? value) => new(value);
    public static implicit operator Utf8String(Memory<byte> value) => new(value);
    public static implicit operator Utf8String(ReadOnlyMemory<byte> value) => new(value);

    public static implicit operator string(in Utf8String value) => value._string ?? "";
    public static implicit operator ReadOnlyMemory<byte>(in Utf8String value) => value._bytes;
    public static implicit operator ReadOnlyMemory<char>(in Utf8String value) => value._string.AsMemory();
    public static implicit operator ReadOnlySpan<byte>(in Utf8String value) => value._bytes.Span;
    public static implicit operator ReadOnlySpan<char>(in Utf8String value) => value._string;

    public override bool Equals(object? obj)
    {
        return obj switch
        {
            string stringValue => Equals(stringValue),
            null => string.IsNullOrEmpty(_string),
            Utf8String utf8String => Equals(utf8String),
            ReadOnlyMemory<byte> byteValue => Equals(byteValue),
            ReadOnlyMemory<char> charValue => Equals(charValue),
            _ => false,
        };
    }

    public override string ToString() => _string ?? "";
    public override int GetHashCode() => _string?.GetHashCode() ?? 0;
    public static bool operator ==(Utf8String left, Utf8String right) => left.Equals(right);
    public static bool operator !=(Utf8String left, Utf8String right) => !left.Equals(right);
}
