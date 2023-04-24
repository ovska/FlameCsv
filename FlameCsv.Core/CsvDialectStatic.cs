using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv;

// separate class to keep statics and throw helpers from generic type
internal static class CsvDialectStatic
{
    internal static readonly ReadOnlyMemory<byte> _crlf = "\r\n"u8.ToArray();
    internal static readonly ReadOnlyMemory<byte> _lf = "\n"u8.ToArray();
    internal static readonly ReadOnlyMemory<byte> _null = "null"u8.ToArray();

    private static readonly CsvDialect<char> _charCRLF = new(',', '"', "\r\n".AsMemory(), default);
    private static readonly CsvDialect<byte> _byteCRLF = new((byte)',', (byte)'"', _crlf, default);

    public static CsvDialect<T> GetDefault<T>()
        where T : unmanaged, IEquatable<T>
    {
        if (typeof(T) == typeof(char))
            return (CsvDialect<T>)(object)_charCRLF;

        if (typeof(T) == typeof(byte))
            return (CsvDialect<T>)(object)_byteCRLF;

        Token<T>.ThrowNotSupportedException();
        return default; // unreachable
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForInvalid(IEnumerable<string> errors)
    {
        throw new CsvConfigurationException($"Invalid CSV dialect: {string.Join(' ', errors)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForDefault()
    {
        throw new CsvConfigurationException("All CSV dialect tokens were uninitialized (separator, quote, newline).");
    }

    public static ReadOnlyMemory<byte> AsBytes(string? value)
    {
        return value switch
        {
            null or "" => default,
            "\r\n" => _crlf,
            "\n" => _lf,
            "null" => _null,
            _ => Encoding.UTF8.GetBytes(value),
        };
    }

    public static string AsString(ReadOnlyMemory<byte> value)
    {
        if (value.IsEmpty)
            return "";

        var span = value.Span;

        if (span[^1] == '\n')
        {
            if (span.Length == 1)
                return "\n";
            if (span.Length == 2 && span[0] == '\r')
                return "\r\n";
        }

        if (span.SequenceEqual(_null.Span))
            return "null";

        return Encoding.UTF8.GetString(span);
    }

    public static byte AsByte(char value, [CallerMemberName] string name = "")
    {
        return value < 128
            ? (byte)value
            : ThrowHelper.ThrowArgumentOutOfRangeException<byte>(name, value, "Cannot convert char to UTF8 byte");
    }
}
