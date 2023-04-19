namespace FlameCsv;

public interface ICsvDialectOptions<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Column delimiter.
    /// </summary>
    T Delimiter { get; set; }

    /// <summary>
    /// String delimiter, allowing other tokens in the dialect to be used in the value.
    /// </summary>
    T Quote { get; set; }

    /// <summary>
    /// Line terminator.
    /// </summary>
    ReadOnlyMemory<T> Newline { get; set; }

    /// <summary>
    /// Escape character. If null, RFC 4180 mode is used. Otherwise, the escape character is used to
    /// escape the following character.
    /// </summary>
    T? Escape { get; set; }
}
