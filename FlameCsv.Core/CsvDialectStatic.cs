using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv;

// separate class to keep statics and throw helpers from generic type
internal static class CsvDialectStatic
{
    internal static readonly ReadOnlyMemory<byte> _crlf = "\r\n"u8.ToArray();

    private static readonly CsvDialect<char> _charCRLF = new(',', '"', "\r\n".AsMemory());
    private static readonly CsvDialect<byte> _byteCRLF = new((byte)',', (byte)'"', _crlf);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalid<T>(
        T delimiter,
        T quote,
        ReadOnlySpan<T> newline)
        where T : unmanaged, IEquatable<T>
    {
        List<string>? errors = null;

        if (delimiter.Equals(default) && quote.Equals(default) && newline.IsEmpty)
        {
            ThrowForDefault();
        }

        if (delimiter.Equals(quote))
        {
            AddError("Delimiter and Quote must not be equal.");
        }

        if (newline.IsEmpty)
        {
            AddError("Newline must not be empty.");
        }
        else
        {
            if (newline.Contains(delimiter))
                AddError("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                AddError("Newline must not contain Quote.");
        }

        if (errors is not null)
            ThrowForInvalid(errors);

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddError(string message) => (errors ??= new()).Add(message);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForInvalid(IEnumerable<string> errors)
    {
        throw new CsvConfigurationException(
            $"Invalid CSV dialect: {string.Join(' ', errors)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForDefault()
    {
        throw new CsvConfigurationException(
            "All CSV dialect tokens were uninitialized (separator, quote, newline).");
    }
}
