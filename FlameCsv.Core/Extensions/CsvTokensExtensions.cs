using System.Text;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

/// <summary>
/// Extensions for manipulating and validating instances of <see cref="CsvTokens{T}"/>.
/// </summary>
public static class CsvTokensExtensions
{
    // cache common whitespace and line breaks
    private static readonly ReadOnlyMemory<byte> s_crlf = "\r\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> s_lf = "\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> s_space = " "u8.ToArray();

    private static ReadOnlyMemory<byte> AsBytes(this string? value) => value switch
    {
        "\n" => s_lf,
        "\r\n" => s_crlf,
        " " => s_space,
        null or "" => ReadOnlyMemory<byte>.Empty,
        _ => Encoding.UTF8.GetBytes(value),
    };

    /// <summary>
    /// Returns a copy of the tokens with the specified string's characters set as whitespace tokens.
    /// </summary>
    public static CsvTokens<char> WithWhitespace(
        in this CsvTokens<char> options,
        string? whitespace)
    {
        return options with { Whitespace = whitespace.AsMemory() };
    }

    /// <summary>
    /// Returns a copy of the tokens with the specified string's characters set as whitespace tokens.
    /// </summary>
    public static CsvTokens<byte> WithWhitespace(
        in this CsvTokens<byte> options,
        string? whitespace)
    {
        return options with { Whitespace = whitespace.AsBytes() };
    }

    /// <summary>
    /// Returns a copy of the tokens with the specified string's characters set as newline tokens.
    /// </summary>
    public static CsvTokens<char> WithNewLine(
        in this CsvTokens<char> options,
        string? newline)
    {
        return options with { NewLine = newline.AsMemory() };
    }

    /// <summary>
    /// Returns a copy of the tokens with the specified string's characters set as newline tokens.
    /// </summary>
    public static CsvTokens<byte> WithNewLine(
        in this CsvTokens<byte> options,
        string? newline)
    {
        return options with { NewLine = newline.AsBytes() };
    }

    /// <summary>
    /// Copies the options and converts chars to UTF8 bytes.
    /// </summary>
    public static CsvTokens<byte> ToUtf8Bytes(in this CsvTokens<char> options)
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
            if (chars.IsEmpty)
                return default;

            var len = Encoding.UTF8.GetByteCount(chars.Span);
            var result = new byte[len];
            _ = Encoding.UTF8.GetBytes(chars.Span, result.AsSpan());
            return result;
        }
    }

    /// <summary>
    /// Copies the options and converts UTF8 bytes to chars.
    /// </summary>
    public static CsvTokens<char> FromUtf8Bytes(in this CsvTokens<byte> options)
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
