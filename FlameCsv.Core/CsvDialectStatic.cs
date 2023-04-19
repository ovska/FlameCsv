using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv;

// separate class to keep statics and throw helpers from generic type
internal static class CsvDialectStatic
{
    internal static readonly ReadOnlyMemory<byte> _crlf = "\r\n"u8.ToArray();

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
}
