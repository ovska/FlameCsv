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
    /// If not empty, the values to be trimmed from the beginning and end of a single CSV column when parsing.
    /// </summary>
    ReadOnlyMemory<T> Whitespace { get; set; }
}
