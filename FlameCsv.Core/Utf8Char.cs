using CommunityToolkit.Diagnostics;

namespace FlameCsv;

/// <summary>
/// Represents an ASCII UTF-8 character.
/// </summary>
internal readonly struct Utf8Char : IEquatable<Utf8Char>, IEquatable<char>, IEquatable<byte>
{
    private readonly byte _value;

    public Utf8Char(char value)
    {
        if (value >= 128)
            ThrowHelper.ThrowArgumentOutOfRangeException<char>(nameof(value), value, "Cannot convert char to UTF8 byte");

        _value = (byte)value;
    }

    public Utf8Char(byte value)
    {
        if (value >= 128)
            ThrowHelper.ThrowArgumentOutOfRangeException<char>(nameof(value), value, "Cannot convert char to UTF8 byte");

        _value = value;
    }

    public bool Equals(Utf8Char other) => _value == other._value;
    public bool Equals(char other) => _value == other;
    public bool Equals(byte other) => _value == other;

    public static implicit operator Utf8Char(char value) => new(value);
    public static implicit operator Utf8Char(byte value) => new(value);
    public static implicit operator char(Utf8Char value) => (char)value._value;
    public static implicit operator byte(Utf8Char value) => value._value;

    public override bool Equals(object? obj)
    {
        return obj switch
        {
            Utf8Char utf8Char => Equals(utf8Char),
            char c => Equals(c),
            byte b => Equals(b),
            _ => false,
        };
    }

    public override int GetHashCode() => _value;
    public static bool operator ==(Utf8Char left, Utf8Char right) => left.Equals(right);
    public static bool operator !=(Utf8Char left, Utf8Char right) => !(left == right);
}
