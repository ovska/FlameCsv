using CommunityToolkit.Diagnostics;

namespace FlameCsv;

/// <summary>
/// Represents a single UTF-8 character.
/// </summary>
public readonly struct Utf8Char
{
    private readonly byte _value;

    public Utf8Char(char value)
    {
        if ((uint)value >= 128)
            ThrowHelper.ThrowArgumentOutOfRangeException<char>(nameof(value), value, "Cannot convert char to UTF8 byte");

        _value = (byte)value;
    }

    public Utf8Char(byte value)
    {
        _value = value;
    }

    public static implicit operator Utf8Char(char value) => new(value);
    public static implicit operator Utf8Char(byte value) => new(value);
    public static implicit operator char(Utf8Char value) => (char)value._value;
    public static implicit operator byte(Utf8Char value) => value._value;
}
