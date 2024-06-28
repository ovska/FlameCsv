namespace FlameCsv;

internal interface ICsvDialectOptions<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Field delimiter.
    /// </summary>
    T Delimiter { get; set; }

    /// <summary>
    /// String delimiter, allowing other tokens in the dialect to be used in the value.
    /// </summary>
    T Quote { get; set; }

    /// <summary>
    /// Line terminator. When empty (the default), automatically detected between <c>\r\n</c> and <c>\n</c> from the first line.
    /// </summary>
    ReadOnlyMemory<T> Newline { get; set; }

    /// <summary>
    /// Whitespace tokens. When writing, values with trailing or leading whitespace are quoted.
    /// When reading, leading and trailing whitespace characters are trimmed.
    /// </summary>
    ReadOnlyMemory<T> Whitespace { get; set; }

    /// <summary>
    /// Escape character. If null, RFC 4180 mode is used. Otherwise, the escape character is used to
    /// escape the following character.
    /// </summary>
    T? Escape { get; set; }
}
