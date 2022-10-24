using System.Text;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

/// <summary>
/// Extensions for manipulating and validating instances of <see cref="CsvParserOptions{T}"/>.
/// </summary>
public static partial class CsvParserOptionsExtensions
{
    /// <summary>
    /// Returns a copy of the options with the specified string's characters set as whitespace tokens.
    /// </summary>
    public static CsvParserOptions<char> WithWhitespace(
        in this CsvParserOptions<char> options,
        string? whitespace)
    {
        return options with
        {
            Whitespace = whitespace.AsMemory(),
        };
    }

    /// <summary>
    /// Returns a copy of the options with the specified string's characters set as whitespace tokens.
    /// </summary>
    public static CsvParserOptions<byte> WithWhitespace(
        in this CsvParserOptions<byte> options,
        string? whitespace)
    {
        return options with
        {
            Whitespace = Encoding.UTF8.GetBytes(whitespace ?? "").AsMemory(),
        };
    }

    /// <summary>
    /// Copies the options and converts chars to UTF8 bytes.
    /// </summary>
    public static CsvParserOptions<byte> ToUtf8Bytes(in this CsvParserOptions<char> options)
    {
        return new()
        {
            Delimiter = ToSingleByte(options.Delimiter),
            StringDelimiter = ToSingleByte(options.StringDelimiter),
            NewLine = ToBytes(options.NewLine),
            Whitespace = ToBytes(options.Whitespace),
        };

        static byte ToSingleByte(char c)
        {
            return c <= 127
                ? (byte)c
                : ThrowHelper.ThrowInvalidOperationException<byte>($"Cannot convert char {c} to single UTF8 byte");
        }

        static ReadOnlyMemory<byte> ToBytes(ReadOnlyMemory<char> chars)
        {
            var len = Encoding.UTF8.GetByteCount(chars.Span);
            var result = new byte[len];
            _ = Encoding.UTF8.GetBytes(chars.Span, result.AsSpan());
            return result;
        }
    }

    /// <summary>
    /// Copies the options and converts UTF8 bytes to chars.
    /// </summary>
    public static CsvParserOptions<char> FromUtf8Bytes(in this CsvParserOptions<byte> options)
    {
        return new()
        {
            Delimiter = ToSingleChar(options.Delimiter),
            StringDelimiter = ToSingleChar(options.StringDelimiter),
            NewLine = ToChars(options.NewLine),
            Whitespace = ToChars(options.Whitespace),
        };

        static char ToSingleChar(byte b)
        {
            return b <= 127
                ? (char)b
                : ThrowHelper.ThrowInvalidOperationException<char>($"Cannot convert UTF8 byte {b} to single char");
        }

        static ReadOnlyMemory<char> ToChars(ReadOnlyMemory<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes.Span).AsMemory();
        }
    }
}
