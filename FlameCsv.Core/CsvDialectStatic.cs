using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv;

// separate class to keep statics and throw helpers from generic type
internal static class CsvDialectStatic
{
    public static CsvDialect<char> Text(
        char delimiter = ',',
        char quote = '"',
        string newline = "\r\n",
        string whitespace = "",
        char? escape = null)
    {
        return new(delimiter, quote, newline.AsMemory(), whitespace.AsMemory(), escape);
    }

    public static CsvDialect<byte> Utf8(
        char delimiter = ',',
        char quote = '"',
        string newline = "\r\n",
        string whitespace = "",
        char? escape = null)
    {
        return new(
            (Utf8Char)delimiter,
            (Utf8Char)quote,
            (Utf8String)newline,
            (Utf8String)whitespace,
            (Utf8Char?)escape);
    }

    private static readonly CsvDialect<char> _charCRLF = new(
        delimiter: ',',
        quote: '"',
        newline: "\r\n".AsMemory(),
        whitespace: default,
        escape: default);

    private static readonly CsvDialect<byte> _byteCRLF = new(
        delimiter: new Utf8Char(','),
        quote: new Utf8Char('"'),
        newline: Utf8String.CRLF,
        whitespace: default,
        escape: default);

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
